using AwesomeAssertions;
using NetArchTest.Rules;

namespace Hospital.ArchitectureTests;

/// <summary>
/// Onion mimarisi: bağımlılıklar yalnızca içeriye doğrudur.
/// Domain ← Application ← (Infrastructure, Persistence) ← WebAPI. Infrastructure ve Persistence birbirini bilmez.
/// </summary>
public class LayerDependencyTests
{
    private const string Application = "Hospital.Application";
    private const string Infrastructure = "Hospital.Infrastructure";
    private const string Persistence = "Hospital.Persistence";
    private const string WebApi = "Hospital.WebAPI";

    [Fact]
    public void Domain_hicbir_katmana_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Persistence, WebApi)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Domain_altyapi_kutuphanelerinden_bagimsiz_olmali()
    {
        var result = Types.InAssembly(Hospital.Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Microsoft.AspNetCore",
                "Mediator",
                "FluentValidation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_dis_katmanlara_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Persistence, WebApi)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_veritabani_kutuphanelerini_bilmemeli()
    {
        var result = Types.InAssembly(Hospital.Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_Persistence_ve_WebAPI_ye_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Infrastructure.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Persistence, WebApi)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Persistence_Infrastructure_ve_WebAPI_ye_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Persistence.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, WebApi)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? string.Empty
            : "İhlal eden tipler: " + string.Join(", ", result.FailingTypeNames);
}
