namespace Hospital.Api.Common;

/// <summary>
/// Minimal API endpoint grubu. Her feature kendi Api klasöründe bu arayüzü uygular;
/// sınıflar tarama ile bulunur, Program.cs değişmez.
/// </summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
