namespace Hospital.Domain.Common;

/// <summary>Domain invariant ihlali (ör. geçersiz durum geçişi). Beklenen iş hataları için Result kullanılır.</summary>
public class DomainException(string message) : Exception(message);
