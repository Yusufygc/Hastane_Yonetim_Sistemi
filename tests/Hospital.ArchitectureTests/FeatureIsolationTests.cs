using FluentAssertions;
using NetArchTest.Rules;

namespace Hospital.ArchitectureTests;

/// <summary>
/// Bir feature başka bir feature'ın handler/command/query/validator tiplerine bağlanamaz.
/// Gerekirse yalnızca o feature'ın soyutlamasını (arayüz) kullanır.
/// </summary>
public class FeatureIsolationTests
{
    private const string FeaturesNamespace = "Hospital.Application.Features";
    private static readonly string[] InternalSuffixes = ["Handler", "Command", "Query", "Validator"];

    [Fact]
    public void Feature_baska_feature_in_ic_tiplerine_bagimli_olmamali()
    {
        var assembly = Hospital.Application.AssemblyReference.Assembly;

        var internalTypesByFeature = assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(FeaturesNamespace + ".", StringComparison.Ordinal))
            .Where(t => InternalSuffixes.Any(suffix => t.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .GroupBy(t => FeatureOf(t.Namespace!))
            .ToDictionary(g => g.Key, g => g.Select(t => t.FullName!).ToArray());

        var violations = new List<string>();

        foreach (var feature in internalTypesByFeature.Keys)
        {
            var foreignTypes = internalTypesByFeature
                .Where(pair => pair.Key != feature)
                .SelectMany(pair => pair.Value)
                .ToArray();

            if (foreignTypes.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceStartingWith($"{FeaturesNamespace}.{feature}")
                .ShouldNot()
                .HaveDependencyOnAny(foreignTypes)
                .GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange(result.FailingTypeNames ?? []);
            }
        }

        violations.Should().BeEmpty("feature'lar birbirinin handler/command/query tiplerine bağlanmamalı");
    }

    private static string FeatureOf(string ns) =>
        ns[(FeaturesNamespace.Length + 1)..].Split('.')[0];
}
