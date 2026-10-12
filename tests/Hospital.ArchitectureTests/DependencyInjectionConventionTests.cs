using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Hospital.ArchitectureTests;

/// <summary>
/// DI kuralı: IServiceCollection genişletme metotları (Add...) her katmanda `Extensions` klasöründe/namespace'inde
/// ve adı `Ext` ile biten statik sınıflarda bulunur. Program.cs yalnızca bu metotları çağırır.
/// </summary>
public class DependencyInjectionConventionTests
{
    private static readonly Assembly[] Layers =
    [
        Hospital.Application.AssemblyReference.Assembly,
        Hospital.Infrastructure.AssemblyReference.Assembly,
        Hospital.Persistence.AssemblyReference.Assembly,
        typeof(Program).Assembly,
    ];

    [Fact]
    public void Servis_kayit_metotlari_Extensions_klasorunde_ve_Ext_sinifinda_olmali()
    {
        var offenders = Layers
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: true, IsSealed: true }                        // static class
                           && type.Namespace is not null
                           && type.Namespace.StartsWith("Hospital.", StringComparison.Ordinal)) // üretilmiş kodu (Mediator) dışla
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(IsServiceCollectionExtension))
            .Where(type => !type.Namespace!.EndsWith(".Extensions", StringComparison.Ordinal)
                           || !type.Name.EndsWith("Ext", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("IServiceCollection genişletmeleri <Katman>/Extensions/<Ad>Ext.cs içinde olmalı");
    }

    [Fact]
    public void Her_katman_kendi_Add_metodunu_sunmali()
    {
        var expected = new Dictionary<string, Assembly>
        {
            ["AddApplication"] = Layers[0],
            ["AddInfrastructure"] = Layers[1],
            ["AddPersistence"] = Layers[2],
            ["AddWebApi"] = Layers[3],
        };

        foreach (var (method, assembly) in expected)
        {
            assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Should().Contain(m => m.Name == method && IsServiceCollectionExtension(m),
                    $"{assembly.GetName().Name} katmanı {method} extension'ını sunmalı");
        }
    }

    private static bool IsServiceCollectionExtension(MethodInfo method) =>
        method.IsDefined(typeof(ExtensionAttribute), inherit: false)
        && method.GetParameters() is [{ } first, ..]
        && first.ParameterType == typeof(IServiceCollection);
}
