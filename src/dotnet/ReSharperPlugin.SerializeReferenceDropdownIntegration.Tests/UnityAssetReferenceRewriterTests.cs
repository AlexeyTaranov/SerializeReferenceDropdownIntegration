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

    [Test]
    public void RewritesSerializeReferenceNamespace()
    {
        var circleType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var squareType = new UnityTypeData("Square", "SRD.Sample", "SerializeReferenceDropdownSample");
        var nestedType = new UnityTypeData("Hexagon", "SRD.Sample.Nested", "SerializeReferenceDropdownSample");
        var otherType = new UnityTypeData("Triangle", "SRD.Other", "SerializeReferenceDropdownSample");
        var lines = new[]
        {
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}",
            "      type: {class: Square, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}",
            "      type: {class: Hexagon, ns: SRD.Sample.Nested, asm: SerializeReferenceDropdownSample}",
            "      type: {class: Triangle, ns: SRD.Other, asm: SerializeReferenceDropdownSample}"
        };

        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            lines,
            new[]
            {
                new UnityReferenceTypeLineData(circleType, 0, false),
                new UnityReferenceTypeLineData(squareType, 1, false),
                new UnityReferenceTypeLineData(nestedType, 2, false),
                new UnityReferenceTypeLineData(otherType, 3, false)
            },
            new UnityReferenceTypePrefabOverrideLineData[0],
            type => type.Namespace == "SRD.Sample" || type.Namespace.StartsWith("SRD.Sample.")
                ? type with { Namespace = "SRD.Renamed" + type.Namespace.Substring("SRD.Sample".Length) }
                : null);
        var rewrittenLines = UnityAssetReferenceRewriter.ApplyChanges(lines, changes);

        Assert.That(changes, Has.Count.EqualTo(3));
        Assert.That(rewrittenLines[0], Is.EqualTo(
            "      type: {class: Circle, ns: SRD.Renamed, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenLines[1], Is.EqualTo(
            "      type: {class: Square, ns: SRD.Renamed, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenLines[2], Is.EqualTo(
            "      type: {class: Hexagon, ns: SRD.Renamed.Nested, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenLines[3], Is.EqualTo(
            "      type: {class: Triangle, ns: SRD.Other, asm: SerializeReferenceDropdownSample}"));
    }

    [Test]
    public void RewritesPrefabOverrideNamespace()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var lines = new[]
        {
            "      propertyPath: 'managedReferences[1988581617954979845]'",
            "      value: SerializeReferenceDropdownSample SRD.Sample.Circle"
        };

        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            lines,
            new UnityReferenceTypeLineData[0],
            new[] { new UnityReferenceTypePrefabOverrideLineData(oldType, 1) },
            type => type.Namespace == "SRD.Sample" ? type with { Namespace = "SRD.Renamed" } : null);
        var rewrittenLines = UnityAssetReferenceRewriter.ApplyChanges(lines, changes);

        Assert.That(changes, Has.Count.EqualTo(1));
        Assert.That(changes[0].NewType, Is.EqualTo(oldType with { Namespace = "SRD.Renamed" }));
        Assert.That(rewrittenLines[1], Is.EqualTo("      value: SerializeReferenceDropdownSample SRD.Renamed.Circle"));
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "UnityAssets", "Assets", fileName);
    }
}
