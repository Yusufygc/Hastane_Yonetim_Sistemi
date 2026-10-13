using AwesomeAssertions;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Auth.Commands.Login;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Application;

public class LoginHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly AuthSettings Settings = new(3, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15), TimeSpan.FromDays(7));

    private readonly FakeUsers _users = new();
    private readonly FakeRefreshTokens _refreshTokens = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokenService = new();

    private LoginHandler CreateHandler() =>
        new(_users, _hasher, new AuthTokenIssuer(_tokenService, _refreshTokens, Settings), _unitOfWork, Settings, new FixedTime(Now));

    private Patient AddPatient()
    {
        var patient = Patient.Register("hasta@example.test", "hash:Test1234x", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 1, 1), null, Now);
        _users.Items.Add(patient);
        return patient;
    }

    [Fact]
    public async Task Dogru_parola_erisim_ve_yenileme_belirteci_doner_son_giris_kaydedilir()
    {
        var patient = AddPatient();

        var result = await CreateHandler().Handle(new LoginCommand("Hasta@Example.Test", "Test1234x"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().StartWith($"jwt-for-{patient.Id}");
        result.Value.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
        result.Value.RefreshTokenExpiresAt.Should().Be(Now.AddDays(7));
        result.Value.User.Role.Should().Be("PATIENT");
        patient.LastLoginAt.Should().Be(Now);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Yenileme_belirtecinin_duz_degeri_saklanmaz_yalnizca_ozeti_saklanir()
    {
        AddPatient();

        var result = await CreateHandler().Handle(new LoginCommand("hasta@example.test", "Test1234x"), default);

        var stored = _refreshTokens.Items.Should().ContainSingle().Subject;
        stored.TokenHash.Should().NotBe(result.Value.RefreshToken);
        stored.TokenHash.Should().Be(RefreshTokenSecret.Hash(result.Value.RefreshToken));
        stored.ExpiresAt.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task Her_giris_farkli_yenileme_belirteci_uretir()
    {
        AddPatient();
        var handler = CreateHandler();

        var first = await handler.Handle(new LoginCommand("hasta@example.test", "Test1234x"), default);
        var second = await handler.Handle(new LoginCommand("hasta@example.test", "Test1234x"), default);

        first.Value.RefreshToken.Should().NotBe(second.Value.RefreshToken);
        _refreshTokens.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Bilinmeyen_eposta_genel_hata_doner_ve_yine_de_hash_maliyeti_oder()
    {
        var result = await CreateHandler().Handle(new LoginCommand("yok@example.test", "Test1234x"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        _hasher.HashCalls.Should().Be(1);
        _refreshTokens.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Yanlis_parola_sayaci_artirir_belirteç_uretmez_ve_genel_hata_doner()
    {
        var patient = AddPatient();

        var result = await CreateHandler().Handle(new LoginCommand(patient.Email, "Yanlis1234"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        patient.FailedLoginCount.Should().Be(1);
        _tokenService.Created.Should().Be(0);
        _refreshTokens.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Esik_asilinca_hesap_kilitlenir_ve_dogru_parola_bile_reddedilir()
    {
        var patient = AddPatient();
        var handler = CreateHandler();

        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(new LoginCommand(patient.Email, "Yanlis1234"), default);
        }

        var result = await handler.Handle(new LoginCommand(patient.Email, "Test1234x"), default);

        patient.IsLockedOut(Now).Should().BeTrue();
        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        _refreshTokens.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Pasif_hesap_genel_hata_doner()
    {
        var patient = AddPatient();
        patient.Deactivate();

        var result = await CreateHandler().Handle(new LoginCommand(patient.Email, "Test1234x"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }
}
