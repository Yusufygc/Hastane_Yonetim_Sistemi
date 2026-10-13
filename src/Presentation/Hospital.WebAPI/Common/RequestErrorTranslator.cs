using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hospital.WebAPI.Common;

/// <summary>Çevrilmiş istek hatası: HTTP durumu, kısa başlık, makine kodu ve (varsa) alan bazlı hatalar.</summary>
public sealed record TranslatedRequestError(
    int Status,
    string Title,
    string Code,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? Detail = null);

/// <summary>
/// ASP.NET Core'un istek gövdesi/parametre bağlama hatalarını (BadHttpRequestException, JsonException) istemcinin
/// anlayacağı Türkçe, alan bazlı hatalara çevirir. Örnek: <c>"birthDate": "11.11.1999"</c> →
/// <c>errors.birthDate: ["Geçerli bir tarih olmalı. Biçim: yyyy-MM-dd (ör. 1999-11-11)."]</c>.
/// Bu hatalar doğrulama (FluentValidation) aşamasından ÖNCE, gövde okunurken oluşur.
/// </summary>
public static partial class RequestErrorTranslator
{
    public const string InvalidBodyCode = "request.invalid_body";
    public const string InvalidJsonCode = "request.invalid_json";
    public const string BodyRequiredCode = "request.body_required";
    public const string InvalidParameterCode = "request.invalid_parameter";
    public const string UnsupportedMediaTypeCode = "request.unsupported_media_type";
    public const string BadRequestCode = "request.bad_request";

    [GeneratedRegex("""Required parameter "(?<type>.+?) (?<name>[\w]+)" was not provided from (?<source>[\w ]+?)\.""", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredParameterRegex();

    [GeneratedRegex("""Failed to bind parameter "(?<type>.+?) (?<name>[\w]+)" from "(?<value>.*)"\.""", RegexOptions.CultureInvariant)]
    private static partial Regex BindParameterRegex();

    [GeneratedRegex("""Expected a supported JSON media type but got "(?<contentType>.*)"\.""", RegexOptions.CultureInvariant)]
    private static partial Regex MediaTypeRegex();

    public static TranslatedRequestError Translate(BadHttpRequestException exception)
    {
        if (exception.InnerException is JsonException json)
        {
            return FromJson(json);
        }

        var message = exception.Message;

        var mediaType = MediaTypeRegex().Match(message);
        if (mediaType.Success || exception.StatusCode == StatusCodes.Status415UnsupportedMediaType)
        {
            var sent = mediaType.Success ? mediaType.Groups["contentType"].Value : "bilinmiyor";
            return new TranslatedRequestError(
                StatusCodes.Status415UnsupportedMediaType,
                "Desteklenmeyen içerik türü",
                UnsupportedMediaTypeCode,
                Errors: null,
                Detail: $"İstek gövdesi `Content-Type: application/json` ile gönderilmelidir (gönderilen: {sent}).");
        }

        if (message.Contains("Implicit body inferred", StringComparison.Ordinal))
        {
            return BodyRequired();
        }

        var required = RequiredParameterRegex().Match(message);
        if (required.Success)
        {
            var source = required.Groups["source"].Value;
            return source.Contains("body", StringComparison.OrdinalIgnoreCase)
                ? BodyRequired()
                : new TranslatedRequestError(
                    StatusCodes.Status400BadRequest,
                    "Zorunlu parametre eksik",
                    InvalidParameterCode,
                    new Dictionary<string, string[]>
                    {
                        [CamelCase(required.Groups["name"].Value)] = [$"Bu parametre zorunludur ({SourceName(source)})."],
                    });
        }

        var bind = BindParameterRegex().Match(message);
        if (bind.Success)
        {
            var expected = FriendlyTypeName(bind.Groups["type"].Value);
            return new TranslatedRequestError(
                StatusCodes.Status400BadRequest,
                "Geçersiz parametre",
                InvalidParameterCode,
                new Dictionary<string, string[]>
                {
                    [CamelCase(bind.Groups["name"].Value)] = [$"'{bind.Groups["value"].Value}' geçerli bir {expected} değil."],
                });
        }

        var status = exception.StatusCode is >= 400 and < 500 ? exception.StatusCode : StatusCodes.Status400BadRequest;
        return new TranslatedRequestError(status, "Geçersiz istek", BadRequestCode, Errors: null, Detail: "İstek okunamadı. İstek biçimini kontrol edin.");
    }

    /// <summary>İstek gövdesindeki JSON hatası: sözdizimi mi, yoksa belirli bir alanın türü/biçimi mi bozuk?</summary>
    public static TranslatedRequestError FromJson(JsonException json)
    {
        var isConversion = json.Message.Contains("could not be converted", StringComparison.Ordinal);

        if (!isConversion && json.LineNumber is not null)
        {
            // Sözdizimi hatası: virgül/tırnak/parantez eksik vb.
            var line = json.LineNumber.Value + 1;
            var position = (json.BytePositionInLine ?? 0) + 1;

            return new TranslatedRequestError(
                StatusCodes.Status400BadRequest,
                "Geçersiz JSON",
                InvalidJsonCode,
                new Dictionary<string, string[]>
                {
                    ["body"] = [$"JSON sözdizimi hatalı (satır {line}, konum {position}). Tırnak, virgül ve parantezleri kontrol edin."],
                });
        }

        var field = ToFieldPath(json.Path);
        return new TranslatedRequestError(
            StatusCodes.Status400BadRequest,
            "İstek gövdesi geçersiz",
            InvalidBodyCode,
            new Dictionary<string, string[]> { [field] = [DescribeConversion(json)] });
    }

    private static TranslatedRequestError BodyRequired() =>
        new(
            StatusCodes.Status400BadRequest,
            "İstek gövdesi zorunlu",
            BodyRequiredCode,
            new Dictionary<string, string[]> { ["body"] = ["İstek gövdesi (JSON) zorunludur."] });

    /// <summary>"$.hours[0].day" → "hours[0].day"; "$" → "body".</summary>
    private static string ToFieldPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
        {
            return "body";
        }

        var trimmed = path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path.TrimStart('$');
        return CamelCasePath(trimmed);
    }

    /// <summary>
    /// System.Text.Json dış mesajında alanın değil, KAPSAYAN sınıfın adını yazar ("...converted to Sample"). Alanın beklenen
    /// türü iç istisnada saklıdır: FormatException ("not in a supported DateOnly format"), InvalidOperationException
    /// ("Cannot get the value of a token type 'Number' as a string"). Enum dönüşümlerinde iç istisna yoktur.
    /// </summary>
    private static string DescribeConversion(JsonException json)
    {
        var inner = json.InnerException?.Message ?? string.Empty;

        return inner switch
        {
            _ when inner.Contains("Guid", StringComparison.Ordinal) => "Geçerli bir GUID olmalı (ör. 3fa85f64-5717-4562-b3fc-2c963f66afa6).",
            _ when inner.Contains("DateOnly", StringComparison.Ordinal) => "Geçerli bir tarih olmalı. Biçim: yyyy-MM-dd (ör. 1999-11-11).",
            _ when inner.Contains("TimeOnly", StringComparison.Ordinal) => "Geçerli bir saat olmalı. Biçim: HH:mm (ör. 08:30).",
            _ when inner.Contains("DateTimeOffset", StringComparison.Ordinal) || inner.Contains("DateTime", StringComparison.Ordinal)
                => "Geçerli bir tarih-saat olmalı (ISO 8601, ör. 2026-11-02T10:00:00Z).",
            _ when inner.Contains("TimeSpan", StringComparison.Ordinal) => "Geçerli bir süre olmalı (ör. 00:30:00).",
            _ when inner.Contains("as a string", StringComparison.Ordinal) => "Metin olmalı; değeri tırnak içinde gönderin (ör. \"5444444444\").",
            _ when inner.Contains("as a boolean", StringComparison.Ordinal) => "true veya false olmalı (tırnaksız).",
            _ when inner.Contains("as a number", StringComparison.Ordinal) => "Sayı olmalı (tırnaksız).",
            _ when inner.Contains("Int16", StringComparison.Ordinal) || inner.Contains("Int32", StringComparison.Ordinal)
                   || inner.Contains("Int64", StringComparison.Ordinal) || inner.Contains("Byte", StringComparison.Ordinal)
                => "Tam sayı olmalı (tırnaksız, ondalıksız).",
            _ when inner.Contains("Decimal", StringComparison.Ordinal) || inner.Contains("Double", StringComparison.Ordinal)
                   || inner.Contains("Single", StringComparison.Ordinal)
                => "Sayı olmalı (tırnaksız; ondalık için nokta kullanın, ör. 250.50).",
            _ when inner.Length == 0 => "Geçersiz değer: izin verilen değerlerden biri olmalı (ör. gün alanında 0=Pazar, 1=Pazartesi … 6=Cumartesi).",
            _ => "Geçersiz değer biçimi.",
        };
    }

    private static string FriendlyTypeName(string type) =>
        type switch
        {
            _ when type.Contains("Guid", StringComparison.OrdinalIgnoreCase) => "GUID",
            _ when type.Contains("int", StringComparison.OrdinalIgnoreCase) => "tam sayı",
            _ when type.Contains("decimal", StringComparison.OrdinalIgnoreCase) || type.Contains("double", StringComparison.OrdinalIgnoreCase) => "sayı",
            _ when type.Contains("bool", StringComparison.OrdinalIgnoreCase) => "true/false değeri",
            _ when type.Contains("DateOnly", StringComparison.OrdinalIgnoreCase) => "tarih (yyyy-MM-dd)",
            _ => type,
        };

    private static string SourceName(string source) =>
        source.Contains("query", StringComparison.OrdinalIgnoreCase) ? "sorgu dizesi"
        : source.Contains("route", StringComparison.OrdinalIgnoreCase) ? "yol"
        : source.Contains("header", StringComparison.OrdinalIgnoreCase) ? "başlık"
        : source;

    private static string CamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(CamelCase));

    private static string CamelCase(string name) =>
        name.Length == 0 || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
