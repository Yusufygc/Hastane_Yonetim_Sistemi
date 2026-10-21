using Hospital.Domain.Common;

namespace Hospital.Domain.Entities.Appointments.States;

/// <summary>
/// Randevu durum makinesi (State Pattern). Her somut durum yalnızca kendi izinli geçişlerini uygular; diğerleri
/// <see cref="DomainException"/> fırlatır. Veritabanında yalnız <c>status</c> metni saklanır; durum nesnesi
/// <see cref="For"/> ile bu değerden üretilir.
/// </summary>
public abstract class AppointmentState
{
    public abstract AppointmentStatus Status { get; }

    /// <summary>Türkçe durum adı (hata iletileri için).</summary>
    public abstract string Label { get; }

    public static AppointmentState For(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => PendingState.Instance,
        AppointmentStatus.Approved => ApprovedState.Instance,
        AppointmentStatus.Rejected => RejectedState.Instance,
        AppointmentStatus.Cancelled => CancelledState.Instance,
        AppointmentStatus.Completed => CompletedState.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Bilinmeyen randevu durumu."),
    };

    /// <summary>Verilen işlem bu durumda uygulanabilir mi? (Zaman/sahiplik kuralları ayrıca denetlenir.)</summary>
    public bool Allows(AppointmentAction action) => action switch
    {
        AppointmentAction.Approve => CanApprove,
        AppointmentAction.Reject => CanReject,
        AppointmentAction.Cancel => CanCancel,
        AppointmentAction.Complete => CanComplete,
        _ => false,
    };

    protected virtual bool CanApprove => false;

    protected virtual bool CanReject => false;

    protected virtual bool CanCancel => false;

    protected virtual bool CanComplete => false;

    /// <summary>Geçiş sonrası durumu döner; geçersizse <see cref="DomainException"/>.</summary>
    public AppointmentStatus Next(AppointmentAction action)
    {
        if (!Allows(action))
        {
            throw new DomainException($"{Label} durumundaki randevuya bu işlem uygulanamaz ({ActionName(action)}).");
        }

        return action switch
        {
            AppointmentAction.Approve => AppointmentStatus.Approved,
            AppointmentAction.Reject => AppointmentStatus.Rejected,
            AppointmentAction.Cancel => AppointmentStatus.Cancelled,
            _ => AppointmentStatus.Completed,
        };
    }

    private static string ActionName(AppointmentAction action) => action switch
    {
        AppointmentAction.Approve => "onaylama",
        AppointmentAction.Reject => "reddetme",
        AppointmentAction.Cancel => "iptal",
        _ => "tamamlama",
    };
}

/// <summary>Hasta talep etti, doktor kararı bekleniyor. Slotu rezerve eder.</summary>
public sealed class PendingState : AppointmentState
{
    public static readonly PendingState Instance = new();

    private PendingState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Pending;

    public override string Label => "Bekleyen";

    protected override bool CanApprove => true;

    protected override bool CanReject => true;

    protected override bool CanCancel => true;
}

/// <summary>Doktor onayladı. Slotu tutar.</summary>
public sealed class ApprovedState : AppointmentState
{
    public static readonly ApprovedState Instance = new();

    private ApprovedState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Approved;

    public override string Label => "Onaylı";

    protected override bool CanCancel => true;

    protected override bool CanComplete => true;
}

public sealed class RejectedState : AppointmentState
{
    public static readonly RejectedState Instance = new();

    private RejectedState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Rejected;

    public override string Label => "Reddedilmiş";
}

public sealed class CancelledState : AppointmentState
{
    public static readonly CancelledState Instance = new();

    private CancelledState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Cancelled;

    public override string Label => "İptal edilmiş";
}

public sealed class CompletedState : AppointmentState
{
    public static readonly CompletedState Instance = new();

    private CompletedState()
    {
    }

    public override AppointmentStatus Status => AppointmentStatus.Completed;

    public override string Label => "Tamamlanmış";
}
