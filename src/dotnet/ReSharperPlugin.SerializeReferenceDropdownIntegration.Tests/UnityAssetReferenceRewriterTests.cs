using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityAssetReferenceRewriterTests
{
    [Test]
    public async Task RewritesSerializeReferenceTypeInCirclePrefab()
    {
        var fixturePath = GetFixturePath("Test Circle Prefab.prefab");
        var references = new List<UnityReferenceTypeLineData>();
        var prefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();
        await UnityAssetReferenceParser.FillReferenceTypesBlocksAsync(fixturePath, references, prefabOverrides);

        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        var rewrittenLines = UnityAssetReferenceRewriter.RewriteLines(
            File.ReadAllLines(fixturePath),
            references.Where(reference => reference.Type == oldType).ToArray(),
            prefabOverrides.Where(prefabOverride => prefabOverride.Type == oldType).ToArray(),
            newType);

        Assert.That(rewrittenLines, Has.Some.Contains("type: {class: RenamedCircle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenLines, Has.None.Contains("type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
    }

    [Test]
    public async Task PreviewChangesDescribesCirclePrefabRewrite()
    {
        var fixturePath = GetFixturePath("Test Circle Prefab.prefab");
        var references = new List<UnityReferenceTypeLineData>();
        var prefabOverrides = new List<UnityReferenceTypePrefabOverrideLineData>();
        await UnityAssetReferenceParser.FillReferenceTypesBlocksAsync(fixturePath, references, prefabOverrides);

        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            File.ReadAllLines(fixturePath),
            references.Where(reference => reference.Type == oldType).ToArray(),
            prefabOverrides.Where(prefabOverride => prefabOverride.Type == oldType).ToArray(),
            newType);

        Assert.That(changes, Has.Count.EqualTo(1));
        Assert.That(changes[0].Kind, Is.EqualTo(UnityAssetReferenceChangeKind.SerializeReference));
        Assert.That(changes[0].LineIndex, Is.EqualTo(52));
        Assert.That(changes[0].OldType, Is.EqualTo(oldType));
        Assert.That(changes[0].NewType, Is.EqualTo(newType));
        Assert.That(changes[0].OldLines.Single(), Does.Contain("type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
        Assert.That(changes[0].NewLines.Single(), Does.Contain("type: {class: RenamedCircle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
    }

    [Test]
    public void RewritesPrefabOverrideType()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        var lines = new[]
        {
            "      propertyPath: 'managedReferences[1988581617954979845]'",
            "      value: SerializeReferenceDropdownSample SRD.Sample.Circle"
        };

        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            lines,
            new UnityReferenceTypeLineData[0],
            new[] { new UnityReferenceTypePrefabOverrideLineData(oldType, 1) },
            newType);
        var rewrittenLines = UnityAssetReferenceRewriter.ApplyChanges(lines, changes);

        Assert.That(changes, Has.Count.EqualTo(1));
        Assert.That(changes[0].Kind, Is.EqualTo(UnityAssetReferenceChangeKind.PrefabOverride));
        Assert.That(rewrittenLines[1], Is.EqualTo("      value: SerializeReferenceDropdownSample SRD.Sample.RenamedCircle"));
    }

    [Test]
    public void RewritesMultilineSerializeReferenceType()
    {
        var oldType = new UnityTypeData("ReallyLongCircle", "SRD.Sample.Really.Long.Namespace", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        var lines = new[]
        {
            "      type: {class: ReallyLongCircle, ns: SRD.Sample.Really.Long.Namespace,",
            "        asm: SerializeReferenceDropdownSample}"
        };

        var rewrittenLines = UnityAssetReferenceRewriter.RewriteLines(
            lines,
            new[] { new UnityReferenceTypeLineData(oldType, 0, true) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        Assert.That(rewrittenLines, Has.Count.EqualTo(2));
        Assert.That(rewrittenLines[0], Is.EqualTo(
            "      type: {class: RenamedCircle, ns: SRD.Sample.Really.Long.Namespace,"));
        Assert.That(rewrittenLines[1], Is.EqualTo(
            "        asm: SerializeReferenceDropdownSample}"));
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "UnityAssets", "Assets", fileName);
    }
}
