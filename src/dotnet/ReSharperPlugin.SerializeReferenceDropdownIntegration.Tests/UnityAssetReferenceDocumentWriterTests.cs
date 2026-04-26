using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Tests;

[TestFixture]
public class UnityAssetReferenceDocumentWriterTests
{
    [Test]
    public async Task ReplacesAndSavesChangedDocument()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        const string originalText =
            "--- !u!114 &1\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}\n";
        var document = new FakeDocument(originalText);
        var session = new FakeDocumentSession(new Dictionary<string, FakeDocument>
        {
            ["Assets/Test.prefab"] = document
        });
        var writer = new UnityAssetReferenceDocumentWriter(session);
        var changes = UnityAssetReferenceRewriter.PreviewChanges(
            originalText.Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        await writer.ApplyChangesAsync(new[]
        {
            new UnityAssetReferenceFileChange("Assets/Test.prefab", changes)
        });

        Assert.That(session.ExecutedCommandNames, Is.EqualTo(new[] { UnityAssetReferenceDocumentWriter.CommandName }));
        Assert.That(document.Text, Does.Contain("type: {class: RenamedCircle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}"));
        Assert.That(document.ReplaceTextCallCount, Is.EqualTo(1));
        Assert.That(document.SaveCallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DoesNotReplaceOrSaveUnchangedDocument()
    {
        const string originalText = "--- !u!114 &1\n  radius: 5\n";
        var document = new FakeDocument(originalText);
        var session = new FakeDocumentSession(new Dictionary<string, FakeDocument>
        {
            ["Assets/Test.prefab"] = document
        });
        var writer = new UnityAssetReferenceDocumentWriter(session);

        await writer.ApplyChangesAsync(new[]
        {
            new UnityAssetReferenceFileChange("Assets/Test.prefab", new UnityAssetReferenceChange[0])
        });

        Assert.That(document.ReplaceTextCallCount, Is.Zero);
        Assert.That(document.SaveCallCount, Is.Zero);
    }

    [Test]
    public async Task AppliesAllFilesInsideSingleUndoableCommand()
    {
        var oldType = new UnityTypeData("Circle", "SRD.Sample", "SerializeReferenceDropdownSample");
        var newType = oldType with { ClassName = "RenamedCircle" };
        const string firstText =
            "--- !u!114 &1\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}\n";
        const string secondText =
            "--- !u!114 &2\n" +
            "      type: {class: Circle, ns: SRD.Sample, asm: SerializeReferenceDropdownSample}\n";
        var firstDocument = new FakeDocument(firstText);
        var secondDocument = new FakeDocument(secondText);
        var session = new FakeDocumentSession(new Dictionary<string, FakeDocument>
        {
            ["Assets/First.prefab"] = firstDocument,
            ["Assets/Second.prefab"] = secondDocument
        });
        var writer = new UnityAssetReferenceDocumentWriter(session);
        var firstChanges = UnityAssetReferenceRewriter.PreviewChanges(
            firstText.Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);
        var secondChanges = UnityAssetReferenceRewriter.PreviewChanges(
            secondText.Split('\n'),
            new[] { new UnityReferenceTypeLineData(oldType, 1, false) },
            new UnityReferenceTypePrefabOverrideLineData[0],
            newType);

        await writer.ApplyChangesAsync(new[]
        {
            new UnityAssetReferenceFileChange("Assets/First.prefab", firstChanges),
            new UnityAssetReferenceFileChange("Assets/Second.prefab", secondChanges)
        });

        Assert.That(session.ExecutedCommandNames, Has.Count.EqualTo(1));
        Assert.That(firstDocument.SaveCallCount, Is.EqualTo(1));
        Assert.That(secondDocument.SaveCallCount, Is.EqualTo(1));
    }

    private sealed class FakeDocumentSession : IUnityAssetReferenceDocumentSession
    {
        private readonly IReadOnlyDictionary<string, FakeDocument> documents;

        public FakeDocumentSession(IReadOnlyDictionary<string, FakeDocument> documents)
        {
            this.documents = documents;
        }

        public List<string> ExecutedCommandNames { get; } = new();

        public void ExecuteUndoableCommand(string commandName, Action action)
        {
            ExecutedCommandNames.Add(commandName);
            action();
        }

        public IUnityAssetReferenceDocument OpenDocument(string filePath)
        {
            return documents[filePath];
        }
    }

    private sealed class FakeDocument : IUnityAssetReferenceDocument
    {
        public FakeDocument(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }
        public int ReplaceTextCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        public void ReplaceText(string text)
        {
            ReplaceTextCallCount++;
            Text = text;
        }

        public void Save()
        {
            SaveCallCount++;
        }
    }
}
