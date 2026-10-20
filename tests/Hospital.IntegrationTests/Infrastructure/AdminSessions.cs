using System.Net;
using AwesomeAssertions;
using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.IntegrationTests.Infrastructure;

/// <summary>Seed ile gelen süper yönetici hesabı (yalnız test değerleri; Faz 2 seeder'ı yapılandırmadan okur).</summary>
public static class TestAdmin
{
    public const string Email = "super.admin@test.local";
    public const string Password = "SuperAdmin123x";
}

/// <summary>Seed'in örnek doktoru ve testlerde yönetici ile açılan doktorların ortak parolası (yalnız test değerleri).</summary>
public static class TestDoctor
{
    public const string SeededEmail = "ornek.doktor@test.local";
    public const string Password = "Doktor12345x";
}

/// <summary>Yönetici oturumu açma ve ön koşul verisi (şube, şube yöneticisi) kurma yardımcıları.</summary>
public static class AdminSessions
{
    public const string BranchManagerPassword = "Sube12345x";

    public static async Task<ApiClient> SuperAdminAsync(ApiFixture fixture)
    {
        var client = fixture.CreateClient();
        await client.LoginAsync(TestAdmin.Email, TestAdmin.Password);
        return client;
    }

    /// <summary>Şube yöneticisi hesabını doğrudan veritabanına yazar ve o hesapla oturum açmış istemciyi döner.</summary>
    public static async Task<ApiClient> BranchManagerAsync(ApiFixture fixture, Guid hospitalId)
    {
        string email;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var organization = await db.Organizations.AsNoTracking().FirstAsync();

            email = $"sube.{Guid.NewGuid():N}@test.local";
            db.Admins.Add(Admin.CreateBranchManager(
                email, hasher.Hash(BranchManagerPassword), "Şube", "Yöneticisi", organization.Id, hospitalId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var client = fixture.CreateClient();
        await client.LoginAsync(email, BranchManagerPassword);
        return client;
    }

    public static async Task<Guid> CreateHospitalAsync(ApiClient superAdmin, string? name = null)
    {
        var response = await superAdmin.PostAsync("/api/hospitals", new
        {
            Name = name ?? $"Şube {Guid.NewGuid():N}",
            Address = "Test Mah. 1",
            Phone = "02120000000",
            TimeZoneId = "Europe/Istanbul",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Seed'in oluşturduğu varsayılan şube ("Merkez Şube").</summary>
    public static async Task<Guid> SeededHospitalIdAsync(ApiClient superAdmin)
    {
        var response = await superAdmin.GetAsync("/api/hospitals");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var hospitals = await ApiClient.ReadJsonAsync(response);
        return hospitals.EnumerateArray()
            .First(h => h.GetProperty("name").GetString() == "Merkez Şube")
            .GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateDepartmentAsync(ApiClient admin, Guid hospitalId, string? name = null)
    {
        var response = await admin.PostAsync(
            $"/api/hospitals/{hospitalId}/departments", new { Name = name ?? $"Bölüm {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateServiceAsync(
        ApiClient admin,
        Guid hospitalId,
        Guid? departmentId = null,
        string? name = null,
        int durationMinutes = 20,
        decimal price = 300m)
    {
        var response = await admin.PostAsync("/api/services", new
        {
            HospitalId = hospitalId,
            DepartmentId = departmentId,
            Name = name ?? $"Hizmet {Guid.NewGuid():N}",
            DurationMinutes = durationMinutes,
            Price = price,
            Currency = "TRY",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Yönetici ile doktor hesabı açar (benzersiz e-posta ve diploma). Kimliği ve e-postasını döner.</summary>
    public static async Task<(Guid Id, string Email)> CreateDoctorAsync(ApiClient admin, Guid departmentId)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = $"doktor.{unique}@test.local";

        var response = await admin.PostAsync("/api/doctors", new
        {
            DepartmentId = departmentId,
            Email = email,
            Password = TestDoctor.Password,
            FirstName = "Mehmet",
            LastName = "Demir",
            Specialty = "Dahiliye",
            DiplomaNo = $"D-{unique}",
            Phone = "05551112233",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return ((await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid(), email);
    }

    public static async Task<ApiClient> LoginAsync(ApiFixture fixture, string email, string password)
    {
        var client = fixture.CreateClient();
        await client.LoginAsync(email, password);
        return client;
    }

    /// <summary>Doktor için hafta içi (Pzt) 09:00-12:00 uygunluk ekler; seed şubesinin saatleri içindedir.</summary>
    public static async Task<Guid> AddAvailabilityAsync(ApiClient doctor, DayOfWeek day = DayOfWeek.Monday, string start = "09:00", string end = "12:00")
    {
        var response = await doctor.PostAsync("/api/doctors/me/availability", new
        {
            Day = day,
            StartTime = start,
            EndTime = end,
            ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ApiClient.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }
}
