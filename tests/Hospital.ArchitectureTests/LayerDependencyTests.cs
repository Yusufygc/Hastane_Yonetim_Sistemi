using FluentAssertions;
using NetArchTest.Rules;

namespace Hospital.ArchitectureTests;

public class LayerDependencyTests
{
    private const string Domain = "Hospital.Domain";
    private const string Application = "Hospital.Application";
    private const string Infrastructure = "Hospital.Infrastructure";
    private const string Api = "Hospital.Api";

    [Fact]
    public void Domain_hicbir_katmana_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_Infrastructure_ve_Api_ye_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Infrastructure_Api_ye_bagimli_olmamali()
    {
        var result = Types.InAssembly(Hospital.Infrastructure.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    internal static string Describe(TestResult result) =>
        result.FailingTypeNames is null
            ? string.Empty
            : "İhlal eden tipler: " + string.Join(", ", result.FailingTypeNames);
}
