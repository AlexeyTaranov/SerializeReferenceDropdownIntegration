using System;
using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityAssetUsagePreviewBuilderTests
{
    [Test]
    public void BuildCreatesStructuredPreviewWithUnityAssetPaths()
    {
        var solutionPath = "/Project";
        var references = new[]
        {
            new UnityAssetUsageReferenceData(
                "/Project/Assets/Prefabs/Item.prefab",
                new[] { 7, 3, 5 }),
            new UnityAssetUsageReferenceData(
                "/Project/Assets/Data/Item.asset",
                new[] { 11 })
        };

        var preview = UnityAssetUsagePreviewBuilder.Build(solutionPath, references);

        Assert.That(preview.FirstAssetPath, Is.EqualTo("Assets/Prefabs/Item.prefab"));
        Assert.That(preview.Files, Has.Count.EqualTo(2));
        Assert.That(preview.Files[0].AssetPath, Is.EqualTo("Assets/Prefabs/Item.prefab"));
        Assert.That(preview.Files[0].ReferencesCount, Is.EqualTo(3));
        Assert.That(preview.Files[0].LineNumbers, Is.EqualTo(new[] { 3, 5, 7 }));
        Assert.That(preview.Message, Does.Contain("Found 4 references in 2 Unity asset files."));
        Assert.That(preview.Message, Does.Contain("Assets/Prefabs/Item.prefab: 3 references, lines 3, 5, 7"));
        Assert.That(preview.Message, Does.Contain("Assets/Data/Item.asset: 1 references, lines 11"));
    }

    [Test]
    public void BuildHandlesEmptyReferences()
    {
        var preview = UnityAssetUsagePreviewBuilder.Build("/Project",
            Array.Empty<UnityAssetUsageReferenceData>());

        Assert.That(preview.Files, Is.Empty);
        Assert.That(preview.FirstAssetPath, Is.Null);
        Assert.That(preview.Message, Is.EqualTo("No Unity asset files reference this type."));
    }

    [Test]
    public void BuildHandlesCancelledScan()
    {
        var preview = UnityAssetUsagePreviewBuilder.Build("/Project", null);

        Assert.That(preview.Files, Is.Empty);
        Assert.That(preview.FirstAssetPath, Is.Null);
        Assert.That(preview.Message, Is.EqualTo("Unity asset usage scan was cancelled."));
    }
}
