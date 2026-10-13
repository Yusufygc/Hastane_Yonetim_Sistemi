using Hospital.Application.Interfaces;
using Hospital.Domain.Entities.Facility;
using Hospital.Domain.Entities.Users;
using Hospital.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hospital.Persistence.Seeding;

/// <summary>
/// İlk kurulum verisi (idempotent). Tek şubeli kullanım için hazır bir kurum + şube oluşturur,
/// isteğe bağlı örnek bölüm/hizmet ekler ve ilk süper yöneticiyi yapılandırmadan (Seed:SuperAdmin) açar.
/// Üretimde süper yönetici bilgileri ortam değişkeni/user-secrets ile verilmelidir.
/// </summary>
internal sealed class DatabaseSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations.OrderBy(o => o.Id).FirstOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            organization = Organization.Create(configuration["Seed:OrganizationName"] ?? "Hastane Grubu");
            db.Organizations.Add(organization);

            var hospital = HospitalBranch.Create(
                organization.Id,
                configuration["Seed:HospitalName"] ?? "Merkez Şube",
                address: null,
                phone: null,
                timeZoneId: configuration["Seed:TimeZone"] ?? "Europe/Istanbul");

            hospital.SetWorkingHours(DefaultWorkingHours());
            db.Hospitals.Add(hospital);

            if (bool.TryParse(configuration["Seed:SampleData"], out var sampleData) && sampleData)
            {
                AddSampleData(hospital);
            }

            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Varsayılan kurum ve şube oluşturuldu.");
        }

        await SeedSuperAdminAsync(organization, cancellationToken);
    }

    private async Task SeedSuperAdminAsync(Organization organization, CancellationToken cancellationToken)
    {
        if (await db.Admins.AnyAsync(cancellationToken))
        {
            return;
        }

        var email = configuration["Seed:SuperAdmin:Email"];
        var password = configuration["Seed:SuperAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Süper yönetici oluşturulmadı: Seed:SuperAdmin:Email ve Seed:SuperAdmin:Password ayarlanmamış.");
            return;
        }

        var admin = Admin.CreateSuperAdmin(
            email,
            passwordHasher.Hash(password),
            configuration["Seed:SuperAdmin:FirstName"] ?? "Sistem",
            configuration["Seed:SuperAdmin:LastName"] ?? "Yöneticisi",
            organization.Id,
            clock.GetUtcNow());

        db.Admins.Add(admin);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("İlk süper yönetici oluşturuldu.");
    }

    private static IEnumerable<WorkingHoursInput> DefaultWorkingHours()
    {
        // Hafta içi 08:00-17:00, Cumartesi 09:00-13:00; Pazar kapalı.
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            yield return new WorkingHoursInput(day, new TimeOnly(8, 0), new TimeOnly(17, 0));
        }

        yield return new WorkingHoursInput(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(13, 0));
    }

    private void AddSampleData(HospitalBranch hospital)
    {
        var internalMedicine = Department.Create(hospital.Id, "Dahiliye");
        var cardiology = Department.Create(hospital.Id, "Kardiyoloji");
        var radiology = Department.Create(hospital.Id, "Radyoloji");
        var laboratory = Department.Create(hospital.Id, "Laboratuvar");
        db.Departments.AddRange(internalMedicine, cardiology, radiology, laboratory);

        db.Services.AddRange(
            Service.Create(hospital.Id, internalMedicine.Id, "Genel Muayene", 15, 500m, "TRY"),
            Service.Create(hospital.Id, cardiology.Id, "Kardiyoloji Muayenesi", 30, 800m, "TRY"),
            Service.Create(hospital.Id, radiology.Id, "Röntgen", 20, 400m, "TRY"),
            Service.Create(hospital.Id, laboratory.Id, "Kan Tahlili", 10, 250m, "TRY"));
    }
}
