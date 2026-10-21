using AwesomeAssertions;
using Hospital.Domain.Common;
using Hospital.Domain.Entities.Appointments;
using Hospital.Domain.Entities.Appointments.States;

namespace Hospital.UnitTests.Domain;

/// <summary>State Pattern: her durumun izinli geçişleri (plan.md §7.4 durum makinesi).</summary>
public class AppointmentStateTests
{
    // (durum, işlem) → beklenen sonraki durum; null = geçersiz geçiş.
    public static TheoryData<AppointmentStatus, AppointmentAction, AppointmentStatus?> Transitions => new()
    {
        { AppointmentStatus.Pending, AppointmentAction.Approve, AppointmentStatus.Approved },
        { AppointmentStatus.Pending, AppointmentAction.Reject, AppointmentStatus.Rejected },
        { AppointmentStatus.Pending, AppointmentAction.Cancel, AppointmentStatus.Cancelled },
        { AppointmentStatus.Pending, AppointmentAction.Complete, null },

        { AppointmentStatus.Approved, AppointmentAction.Approve, null },
        { AppointmentStatus.Approved, AppointmentAction.Reject, null },
        { AppointmentStatus.Approved, AppointmentAction.Cancel, AppointmentStatus.Cancelled },
        { AppointmentStatus.Approved, AppointmentAction.Complete, AppointmentStatus.Completed },

        { AppointmentStatus.Rejected, AppointmentAction.Approve, null },
        { AppointmentStatus.Rejected, AppointmentAction.Reject, null },
        { AppointmentStatus.Rejected, AppointmentAction.Cancel, null },
        { AppointmentStatus.Rejected, AppointmentAction.Complete, null },

        { AppointmentStatus.Cancelled, AppointmentAction.Approve, null },
        { AppointmentStatus.Cancelled, AppointmentAction.Reject, null },
        { AppointmentStatus.Cancelled, AppointmentAction.Cancel, null },
        { AppointmentStatus.Cancelled, AppointmentAction.Complete, null },

        { AppointmentStatus.Completed, AppointmentAction.Approve, null },
        { AppointmentStatus.Completed, AppointmentAction.Reject, null },
        { AppointmentStatus.Completed, AppointmentAction.Cancel, null },
        { AppointmentStatus.Completed, AppointmentAction.Complete, null },
    };

    [Theory]
    [MemberData(nameof(Transitions))]
    public void Gecis_tablosu_planla_birebir_ayni(AppointmentStatus from, AppointmentAction action, AppointmentStatus? expected)
    {
        var state = AppointmentState.For(from);

        state.Allows(action).Should().Be(expected is not null);

        if (expected is { } next)
        {
            state.Next(action).Should().Be(next);
        }
        else
        {
            var act = () => state.Next(action);
            act.Should().Throw<DomainException>();
        }
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending, typeof(PendingState))]
    [InlineData(AppointmentStatus.Approved, typeof(ApprovedState))]
    [InlineData(AppointmentStatus.Rejected, typeof(RejectedState))]
    [InlineData(AppointmentStatus.Cancelled, typeof(CancelledState))]
    [InlineData(AppointmentStatus.Completed, typeof(CompletedState))]
    public void Her_durum_kendi_somut_state_sinifina_cozulur(AppointmentStatus status, Type expected)
    {
        var state = AppointmentState.For(status);

        state.Should().BeOfType(expected);
        state.Status.Should().Be(status);
        state.Label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Bilinmeyen_durum_reddedilir()
    {
        var act = () => AppointmentState.For((AppointmentStatus)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending, true)]
    [InlineData(AppointmentStatus.Approved, true)]
    [InlineData(AppointmentStatus.Rejected, false)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.Completed, false)]
    public void Yalniz_bekleyen_ve_onayli_slotu_tutar(AppointmentStatus status, bool holds) =>
        AppointmentStatusNames.HoldsSlot(status).Should().Be(holds);

    [Theory]
    [InlineData("pending", true)]
    [InlineData("APPROVED", true)]
    [InlineData("Completed", true)]
    [InlineData("bekliyor", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("99", false)]
    public void Durum_metni_ayristirilir(string? value, bool valid) =>
        AppointmentStatusNames.TryParse(value, out _).Should().Be(valid);
}
