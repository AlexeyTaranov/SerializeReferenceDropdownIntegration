using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityAssetReferenceParserTests
{
    private static readonly string[] UnityAssetExtensions = { ".unity", ".prefab", ".asset" };

    [Test]
    public async Task FixturesContainSerializableReferenceData()
    {
        var result = await ParseFixtureAssetsAsync();

        Assert.That(result.Files, Is.Not.Empty, "No Unity fixture files were found.");
        Assert.That(result.References.Count + result.PrefabOverrides.Count, Is.GreaterThan(0),
            "No SerializeReference data was parsed from Unity fixtures.");

        PrintSummary(result.References, result.PrefabOverrides);
    }

    [Test]
    public async Task CirclePrefabContainsExpectedSerializeReferenceType()
    {
        var result = await ParseFixtureAssetsAsync();
        var expectedType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");

        Assert.That(result.References.Select(reference => reference.Type), Does.Contain(expectedType));
    }

    private static async Task<ParseResult> ParseFixtureAssetsAsync()
    {
        var assetsRoot = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "UnityAssets", "Assets");
        Assert.That(Directory.Exists(assetsRoot), Is.True, $"Unity fixture Assets folder was not found: {assetsRoot}");

        var files = Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => UnityAssetExtensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var allReferences = new List<UnityReferenceTypeLineData>();
        var allPrefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();

        foreach (var file in files)
        {
            var references = new List<UnityReferenceTypeLineData>();
            var prefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();
            await UnityAssetReferenceParser.FillReferenceTypesBlocksAsync(file, references, prefabOverrides);

            allReferences.AddRange(references);
            allPrefabOverrides.AddRange(prefabOverrides);

            PrintFileResult(assetsRoot, file, references, prefabOverrides);
        }

        return new ParseResult(files, allReferences, allPrefabOverrides);
    }

    private static void PrintFileResult(string assetsRoot, string file,
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides)
    {
        var relativePath = file.Substring(assetsRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        TestContext.Out.WriteLine(relativePath);

        foreach (var reference in references)
        {
            TestContext.Out.WriteLine(
                $"  reference line {reference.LineIndex}: {reference.Type.BuildSerializeReferenceTypeString()}");
        }

        foreach (var prefabOverride in prefabOverrides)
        {
            TestContext.Out.WriteLine(
                $"  prefab override line {prefabOverride.LineIndex}: {prefabOverride.Type.BuildSerializeReferenceTypeString()}");
        }
    }

    private static void PrintSummary(
        IReadOnlyCollection<UnityReferenceTypeLineData> references,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> prefabOverrides)
    {
        TestContext.Out.WriteLine("Summary");
        TestContext.Out.WriteLine($"  references: {references.Count}");
        TestContext.Out.WriteLine($"  prefab overrides: {prefabOverrides.Count}");

        var types = references.Select(reference => reference.Type)
            .Concat(prefabOverrides.Select(prefabOverride => prefabOverride.Type))
            .GroupBy(type => type)
            .OrderBy(group => group.Key.AssemblyName, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Namespace, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ClassName, StringComparer.Ordinal);

        foreach (var typeGroup in types)
        {
            TestContext.Out.WriteLine($"  {typeGroup.Count()} x {typeGroup.Key.BuildSerializeReferenceTypeString()}");
        }
    }

    private readonly record struct ParseResult(
        IReadOnlyCollection<string> Files,
        IReadOnlyCollection<UnityReferenceTypeLineData> References,
        IReadOnlyCollection<UnityReferenceTypePrefabOverrideLineData> PrefabOverrides);
}
