using System.Text.Json;
using AwesomeAssertions;
using Hospital.WebAPI.Common;
using Microsoft.AspNetCore.Http;

namespace Hospital.IntegrationTests;

/// <summary>Saf (HTTP'siz) çevirici testleri: gerçek System.Text.Json hatalarının nasıl çevrildiği.</summary>
public class RequestErrorTranslatorTests
{
    private sealed record Sample(Guid? Id, DateOnly Date, string Name, int Count, bool Flag, DayOfWeek Day, decimal Price, TimeOnly Time);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static TranslatedRequestError TranslateJson(string json)
    {
        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sample>(json, Web));
        return RequestErrorTranslator.FromJson(exception);
    }

    [Theory]
    [InlineData("""{"id":"abc"}""", "id", "GUID")]
    [InlineData("""{"date":"11.11.1999"}""", "date", "yyyy-MM-dd")]
    // Sayı gönderilen tarih alanı: System.Text.Json yalnızca "sayıyı metin olarak okuyamadı" der, hedef türü bildirmez.
    [InlineData("""{"date":1999}""", "date", "Metin olmalı")]
    [InlineData("""{"name":5444444444}""", "name", "Metin olmalı")]
    [InlineData("""{"count":"x"}""", "count", "Tam sayı")]
    [InlineData("""{"count":1.5}""", "count", "Tam sayı")]
    [InlineData("""{"flag":"evet"}""", "flag", "true veya false")]
    [InlineData("""{"day":"Pazartesi"}""", "day", "0=Pazar")]
    [InlineData("""{"price":"abc"}""", "price", "Sayı olmalı")]
    [InlineData("""{"time":"25:99"}""", "time", "HH:mm")]
    public void Tur_donusum_hatasi_dogru_alan_ve_beklenen_turle_aciklanir(string json, string field, string expected)
    {
        var error = TranslateJson(json);

        error.Status.Should().Be(StatusCodes.Status400BadRequest);
        error.Code.Should().Be(RequestErrorTranslator.InvalidBodyCode);
        error.Errors.Should().ContainKey(field);
        error.Errors![field].Single().Should().Contain(expected);
    }

    [Fact]
    public void Sozdizimi_hatasi_satir_ve_konumla_bildirilir()
    {
        var error = TranslateJson("{\n  \"name\": \"a\",\n}");

        error.Code.Should().Be(RequestErrorTranslator.InvalidJsonCode);
        error.Errors!["body"].Single().Should().Contain("satır 3");
    }

    [Fact]
    public void Ic_ice_alan_yolu_camelCase_nokta_ve_indeksle_gelir()
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<List<Sample>>("""[{"name":"a"},{"count":"x"}]""", Web));

        var error = RequestErrorTranslator.FromJson(exception);

        error.Errors.Should().ContainKey("[1].count");
    }

    [Fact]
    public void BadHttpRequestException_JsonException_ise_alan_bazli_hataya_cevrilir()
    {
        var inner = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sample>("""{"date":"x"}""", Web));
        var bad = new BadHttpRequestException("Failed to read parameter \"Sample request\" from the request body as JSON.", StatusCodes.Status400BadRequest, inner);

        var error = RequestErrorTranslator.Translate(bad);

        error.Errors.Should().ContainKey("date");
    }

    [Theory]
    [InlineData("Required parameter \"int count\" was not provided from query string.", "count", "zorunludur")]
    [InlineData("Failed to bind parameter \"Guid id\" from \"abc\".", "id", "'abc' geçerli bir GUID değil")]
    [InlineData("Failed to bind parameter \"Nullable<Guid> hospitalId\" from \"x\".", "hospitalId", "GUID")]
    public void Parametre_baglama_hatalari_parametre_adini_belirtir(string message, string field, string expected)
    {
        var error = RequestErrorTranslator.Translate(new BadHttpRequestException(message));

        error.Code.Should().Be(RequestErrorTranslator.InvalidParameterCode);
        error.Errors![field].Single().Should().Contain(expected);
    }

    [Theory]
    [InlineData("Required parameter \"LoginRequest request\" was not provided from body.")]
    [InlineData("Implicit body inferred for parameter \"request\" but no body was provided. Did you mean to use a Service instead?")]
    public void Eksik_govde_govde_zorunlu_hatasina_cevrilir(string message)
    {
        var error = RequestErrorTranslator.Translate(new BadHttpRequestException(message));

        error.Code.Should().Be(RequestErrorTranslator.BodyRequiredCode);
        error.Errors.Should().ContainKey("body");
    }

    [Fact]
    public void Bilinmeyen_istek_hatasi_genel_ama_Turkce_mesaja_duser()
    {
        var error = RequestErrorTranslator.Translate(new BadHttpRequestException("Garip bir hata", StatusCodes.Status400BadRequest));

        error.Code.Should().Be(RequestErrorTranslator.BadRequestCode);
        error.Detail.Should().Contain("İstek okunamadı");
    }
}
