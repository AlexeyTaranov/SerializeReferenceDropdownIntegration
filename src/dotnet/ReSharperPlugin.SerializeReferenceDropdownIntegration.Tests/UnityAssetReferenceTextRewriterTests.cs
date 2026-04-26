using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityAssetReferenceTextRewriterTests
{
    [Test]
    public void AppliesSerializeReferenceChangeToDocumentText()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        const string originalText =
            "--- !u!114 &1\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}\n" +
            "  radius: 5\n";
        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            originalText.Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        var rewrittenText = UnityAssetReferenceTextRewriter.ApplyChanges(originalText, changes);

        Assert.That(rewrittenText, Does.Contain(
            "type: {class: RenamedCircle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenText, Does.Not.Contain(
            "type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
        Assert.That(rewrittenText, Does.EndWith("\n"));
    }

    [Test]
    public void PreservesCrLfLineEndingsForSavedDocumentText()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        const string originalText =
            "--- !u!114 &1\r\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}\r\n" +
            "  radius: 5\r\n";
        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            originalText.Replace("\r\n", "\n").Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        var rewrittenText = UnityAssetReferenceTextRewriter.ApplyChanges(originalText, changes);

        Assert.That(rewrittenText, Does.Contain("\r\n"));
        Assert.That(rewrittenText.Replace("\r\n", string.Empty), Does.Not.Contain("\n"));
        Assert.That(rewrittenText, Does.EndWith("\r\n"));
    }

    [Test]
    public void DoesNotAddFinalNewlineWhenOriginalTextDoesNotHaveOne()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        const string originalText =
            "--- !u!114 &1\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}";
        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            originalText.Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        var rewrittenText = UnityAssetReferenceTextRewriter.ApplyChanges(originalText, changes);

        Assert.That(rewrittenText, Does.Not.EndWith("\n"));
    }

    [Test]
    public void ReturnsOriginalTextWhenThereAreNoChanges()
    {
        const string originalText =
            "--- !u!114 &1\r\n" +
            "  radius: 5\r\n";

        var rewrittenText = UnityAssetReferenceTextRewriter.ApplyChanges(originalText,
            new UnityAssetReferenceChange[0]);

        Assert.That(rewrittenText, Is.EqualTo(originalText));
    }
}
