using AwesomeAssertions;
using Hospital.Application.Interfaces;
using Hospital.Application.Features.Auth;
using Hospital.Application.Features.Auth.Commands.Login;
using Hospital.Domain.Entities.Users;

namespace Hospital.UnitTests.Application;

public class LoginHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeUsers _users = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private LoginHandler CreateHandler() =>
        new(_users, _hasher, _unitOfWork, new AuthSettings(3, TimeSpan.FromMinutes(15)), new FixedTime(Now));

    private Patient AddPatient()
    {
        var patient = Patient.Register("hasta@example.test", "hash:Test1234x", "Ayşe", "Yılmaz", "10000000146", new DateOnly(1990, 1, 1), null, Now);
        _users.Items.Add(patient);
        return patient;
    }

    [Fact]
    public async Task Dogru_parola_kullanici_bilgisini_doner_ve_son_giris_kaydedilir()
    {
        var patient = AddPatient();

        var result = await CreateHandler().Handle(new LoginCommand("Hasta@Example.Test", "Test1234x"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(patient.Id);
        result.Value.Role.Should().Be("PATIENT");
        result.Value.SecurityStamp.Should().Be(patient.SecurityStamp);
        patient.LastLoginAt.Should().Be(Now);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Bilinmeyen_eposta_genel_hata_doner_ve_yine_de_hash_maliyeti_oder()
    {
        var result = await CreateHandler().Handle(new LoginCommand("yok@example.test", "Test1234x"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        _hasher.HashCalls.Should().Be(1);
    }

    [Fact]
    public async Task Yanlis_parola_sayaci_artirir_ve_genel_hata_doner()
    {
        var patient = AddPatient();

        var result = await CreateHandler().Handle(new LoginCommand(patient.Email, "Yanlis1234"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
        patient.FailedLoginCount.Should().Be(1);
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
    }

    [Fact]
    public async Task Pasif_hesap_genel_hata_doner()
    {
        var patient = AddPatient();
        patient.Deactivate();

        var result = await CreateHandler().Handle(new LoginCommand(patient.Email, "Test1234x"), default);

        result.Error.Should().Be(AuthErrors.InvalidCredentials);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(u => u.Email == normalizedEmail));

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(u => u.Email == normalizedEmail));

        public Task<bool> PatientIdentityNumberExistsAsync(string identityNumber, CancellationToken cancellationToken) =>
            Task.FromResult(Items.OfType<Patient>().Any(p => p.IdentityNumber == identityNumber));

        public void Add(User user) => Items.Add(user);
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public int HashCalls { get; private set; }

        public string Hash(string password)
        {
            HashCalls++;
            return $"hash:{password}";
        }

        public bool Verify(string passwordHash, string password) => passwordHash == $"hash:{password}";
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
