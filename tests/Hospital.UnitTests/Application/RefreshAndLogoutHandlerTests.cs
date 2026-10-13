using AwesomeAssertions;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Auth.Commands.Logout;
using Hospital.Application.Features.Auth.Commands.RefreshToken;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Application;

public class RefreshAndLogoutHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly AuthSettings Settings = new(5, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15), TimeSpan.FromDays(7));

    private readonly FakeUsers _users = new();
    private readonly FakeRefreshTokens _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTokenService _tokenService = new();
    private readonly FixedTime _clock = new(Start);

    private RefreshTokenHandler CreateRefreshHandler() =>
        new(_refreshTokens, _users, new AuthTokenIssuer(_tokenService, _refreshTokens, Settings), _unitOfWork, _clock);

    private (Patient Patient, string Plain) SeedUserWithToken()
    {
        var patient = Patient.Register("hasta@example.test", "hash", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 1, 1), null, Start);
        _users.Items.Add(patient);

        var plain = RefreshTokenSecret.Generate();
        _refreshTokens.Items.Add(RefreshToken.Issue(patient.Id, RefreshTokenSecret.Hash(plain), Start, Settings.RefreshTokenLifetime));
        return (patient, plain);
    }

    // ---------- Refresh ----------

    [Fact]
    public async Task Gecerli_yenileme_belirteci_dondurulur_eskisi_iptal_olur_ve_yerine_gecen_kaydedilir()
    {
        var (_, plain) = SeedUserWithToken();
        _clock.Now = Start.AddHours(1);

        var result = await CreateRefreshHandler().Handle(new RefreshTokenCommand(plain), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().NotBe(plain);

        var old = _refreshTokens.Items.Single(t => t.TokenHash == RefreshTokenSecret.Hash(plain));
        var replacement = _refreshTokens.Items.Single(t => t.TokenHash == RefreshTokenSecret.Hash(result.Value.RefreshToken));
        old.IsRevoked.Should().BeTrue();
        old.RevokedAt.Should().Be(_clock.Now);
        old.ReplacedByTokenId.Should().Be(replacement.Id);
        replacement.IsActive(_clock.Now).Should().BeTrue();
    }

    [Fact]
    public async Task Bilinmeyen_yenileme_belirteci_genel_hata_doner()
    {
        SeedUserWithToken();

        var result = await CreateRefreshHandler().Handle(new RefreshTokenCommand("bilinmeyen"), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        _tokenService.Created.Should().Be(0);
    }

    [Fact]
    public async Task Suresi_dolmus_yenileme_belirteci_reddedilir()
    {
        var (_, plain) = SeedUserWithToken();
        _clock.Now = Start.AddDays(8);

        var result = await CreateRefreshHandler().Handle(new RefreshTokenCommand(plain), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    [Fact]
    public async Task Iptal_edilmis_belirtecin_yeniden_kullanimi_kullanicinin_tum_etkin_belirteclerini_iptal_eder()
    {
        var (patient, plain) = SeedUserWithToken();
        var handler = CreateRefreshHandler();

        var rotated = await handler.Handle(new RefreshTokenCommand(plain), default); // plain artık iptal
        rotated.IsSuccess.Should().BeTrue();

        var replay = await handler.Handle(new RefreshTokenCommand(plain), default);   // saldırgan eskiyi sunar

        replay.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        _refreshTokens.Items.Where(t => t.UserId == patient.Id).Should().OnlyContain(t => t.IsRevoked);
    }

    [Fact]
    public async Task Pasif_hesabin_yenileme_belirteci_reddedilir_ve_iptal_edilir()
    {
        var (patient, plain) = SeedUserWithToken();
        patient.Deactivate();

        var result = await CreateRefreshHandler().Handle(new RefreshTokenCommand(plain), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
        _refreshTokens.Items.Single().IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Kilitli_hesabin_yenileme_belirteci_reddedilir()
    {
        var (patient, plain) = SeedUserWithToken();
        for (var i = 0; i < 5; i++)
        {
            patient.RecordFailedLogin(Start, 5, TimeSpan.FromMinutes(15));
        }

        var result = await CreateRefreshHandler().Handle(new RefreshTokenCommand(plain), default);

        result.Error.Should().Be(AuthErrors.InvalidRefreshToken);
    }

    // ---------- Logout ----------

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;

        public string? Email => null;

        public string? Role => null;

        public Guid? OrganizationId => null;

        public Guid? HospitalId => null;

        public string? AccessLevel => null;
    }

    private LogoutHandler CreateLogoutHandler(Guid? currentUserId) =>
        new(_refreshTokens, new FakeCurrentUser(currentUserId), _unitOfWork, _clock);

    [Fact]
    public async Task Cikis_kendi_yenileme_belirtecini_iptal_eder()
    {
        var (patient, plain) = SeedUserWithToken();

        var result = await CreateLogoutHandler(patient.Id).Handle(new LogoutCommand(plain), default);

        result.IsSuccess.Should().BeTrue();
        _refreshTokens.Items.Single().IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Cikis_baskasinin_belirtecini_iptal_etmez_ama_basarili_gorunur()
    {
        var (_, plain) = SeedUserWithToken();

        var result = await CreateLogoutHandler(Guid.NewGuid()).Handle(new LogoutCommand(plain), default);

        result.IsSuccess.Should().BeTrue();
        _refreshTokens.Items.Single().IsRevoked.Should().BeFalse();
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Cikis_bilinmeyen_belirteçte_de_basarilidir()
    {
        var result = await CreateLogoutHandler(Guid.NewGuid()).Handle(new LogoutCommand("yok"), default);

        result.IsSuccess.Should().BeTrue();
    }
}
