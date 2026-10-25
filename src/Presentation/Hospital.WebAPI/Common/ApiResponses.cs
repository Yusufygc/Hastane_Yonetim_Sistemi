namespace Hospital.WebAPI.Common;

// API'nin adlandırılmış yanıt gövdeleri: anonim nesneler yerine kayıt kullanılır ki OpenAPI sözleşmesi (docs/api/openapi.v1.json)
// gövde şemasını belgeleyebilsin. JSON biçimi değişmez (camelCase).

/// <summary>201 Created gövdesi: oluşturulan kaynağın kimliği.</summary>
public sealed record CreatedResponse(Guid Id);

/// <summary>Hasta kaydı 201 gövdesi: oluşan kullanıcının kimliği.</summary>
public sealed record UserCreatedResponse(Guid UserId);

/// <summary><c>GET /api/auth/me</c> gövdesi: oturumdaki kullanıcının belirteçten okunan kimliği.</summary>
public sealed record MeResponse(Guid? Id, string? Email, string? Role);
