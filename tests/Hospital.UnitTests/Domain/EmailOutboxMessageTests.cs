using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Notifications;

namespace Hospital.UnitTests.Domain;

public class EmailOutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static EmailOutboxMessage NewMessage(string to = "  hasta@example.test ") =>
        EmailOutboxMessage.Create("olay:patient", to, " Ayşe Yılmaz ", " Konu ", " İçerik ", "appointment.requested.patient", Now);

    [Fact]
    public void Create_bekleyen_mesaj_olusturur_alanlari_normallestirir()
    {
        var message = NewMessage();

        message.Status.Should().Be(EmailStatus.Pending);
        message.AttemptCount.Should().Be(0);
        message.ToAddress.Should().Be("hasta@example.test");
        message.ToName.Should().Be("Ayşe Yılmaz");
        message.Subject.Should().Be("Konu");
        message.Body.Should().Be("İçerik");
        message.NextAttemptAt.Should().Be(Now); // hemen gönderilebilir
        message.SentAt.Should().BeNull();
        message.LastError.Should().BeNull();
        message.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("adres-degil")]
    public void Create_gecersiz_alici_DomainException_firlatir(string to)
    {
        var act = () => NewMessage(to);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_bos_konu_veya_anahtar_reddedilir()
    {
        ((Action)(() => EmailOutboxMessage.Create("k", "a@b.test", null, " ", "x", "t", Now))).Should().Throw<DomainException>();
        ((Action)(() => EmailOutboxMessage.Create(" ", "a@b.test", null, "s", "x", "t", Now))).Should().Throw<DomainException>();
        ((Action)(() => EmailOutboxMessage.Create("k", "a@b.test", null, "s", " ", "t", Now))).Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkSent_gonderildi_yapar_deneme_sayar_hatayi_temizler()
    {
        var message = NewMessage();

        message.MarkSent(Now.AddSeconds(5));

        message.Status.Should().Be(EmailStatus.Sent);
        message.AttemptCount.Should().Be(1);
        message.SentAt.Should().Be(Now.AddSeconds(5));
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void Gonderilmis_mesaj_tekrar_gonderilmis_veya_basarisiz_isaretlenemez()
    {
        var message = NewMessage();
        message.MarkSent(Now);

        ((Action)(() => message.MarkSent(Now))).Should().Throw<DomainException>();
        ((Action)(() => message.RegisterFailure("x", Now, RetryPolicy.Default))).Should().Throw<DomainException>();
        message.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void Basarisiz_deneme_ussel_geri_cekilmeyle_bekleyen_kalir()
    {
        var message = NewMessage();
        var policy = new RetryPolicy(MaxAttempts: 10, BaseDelay: TimeSpan.FromSeconds(30), MaxDelay: TimeSpan.FromHours(1));
        var expectedDelays = new[] { 30, 60, 120, 240, 480 };

        foreach (var (delay, index) in expectedDelays.Select((d, i) => (d, i)))
        {
            var failedAt = Now.AddMinutes(index);
            message.RegisterFailure("SMTP hatası", failedAt, policy);

            message.Status.Should().Be(EmailStatus.Pending);
            message.AttemptCount.Should().Be(index + 1);
            message.NextAttemptAt.Should().Be(failedAt.AddSeconds(delay));
            message.LastError.Should().Be("SMTP hatası");
        }
    }

    [Fact]
    public void Geri_cekilme_azami_gecikmeyle_sinirlanir()
    {
        var policy = new RetryPolicy(MaxAttempts: 50, BaseDelay: TimeSpan.FromSeconds(30), MaxDelay: TimeSpan.FromMinutes(10));

        policy.DelayAfter(1).Should().Be(TimeSpan.FromSeconds(30));
        policy.DelayAfter(5).Should().Be(TimeSpan.FromSeconds(480));
        policy.DelayAfter(6).Should().Be(TimeSpan.FromMinutes(10)); // 960 sn → sınır 600 sn
        policy.DelayAfter(40).Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Deneme_siniri_asilinca_FAILED_olur_ve_yeniden_denenmez()
    {
        var message = NewMessage();
        var policy = new RetryPolicy(MaxAttempts: 3, BaseDelay: TimeSpan.FromSeconds(30), MaxDelay: TimeSpan.FromHours(1));

        message.RegisterFailure("1", Now, policy);
        message.RegisterFailure("2", Now, policy);
        message.Status.Should().Be(EmailStatus.Pending);
        message.RegisterFailure("üçüncü hata", Now, policy);

        message.Status.Should().Be(EmailStatus.Failed);
        message.AttemptCount.Should().Be(3);
        message.LastError.Should().Be("üçüncü hata");
        ((Action)(() => message.RegisterFailure("4", Now, policy))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Cok_uzun_hata_iletisi_kisaltilarak_saklanir()
    {
        var message = NewMessage();

        message.RegisterFailure(new string('x', 5000), Now, RetryPolicy.Default);

        message.LastError!.Length.Should().Be(EmailOutboxMessage.ErrorMaxLength);
    }

    [Theory]
    [InlineData("PENDING", EmailStatus.Pending)]
    [InlineData("sent", EmailStatus.Sent)]
    [InlineData("Failed", EmailStatus.Failed)]
    public void Durum_metni_ayristirilir_ve_buyuk_harfle_yazilir(string text, EmailStatus status)
    {
        EmailStatusNames.Parse(text).Should().Be(status);
        EmailStatusNames.From(status).Should().Be(status.ToString().ToUpperInvariant());
    }
}
