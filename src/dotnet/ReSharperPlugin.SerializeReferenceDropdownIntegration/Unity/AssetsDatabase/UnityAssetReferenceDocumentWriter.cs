using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public readonly record struct UnityAssetReferenceFileChange(
    string FilePath,
    IReadOnlyList<UnityAssetReferenceChange> Changes);

public interface IUnityAssetReferenceDocumentSession
{
    void ExecuteUndoableCommand(string commandName, Action action);
    IUnityAssetReferenceDocument OpenDocument(string filePath);
}

public interface IUnityAssetReferenceDocument
{
    string Text { get; }
    void ReplaceText(string text);
    void Save();
}

public class UnityAssetReferenceDocumentWriter
{
    public const string CommandName = "Modify Unity asset references";

    private readonly IUnityAssetReferenceDocumentSession documentSession;

    public UnityAssetReferenceDocumentWriter(IUnityAssetReferenceDocumentSession documentSession)
    {
        this.documentSession = documentSession;
    }

    public Task ApplyChangesAsync(IReadOnlyList<UnityAssetReferenceFileChange> fileChanges)
    {
        documentSession.ExecuteUndoableCommand(CommandName, () =>
        {
            foreach (var fileChange in fileChanges)
            {
                ApplyFileChanges(fileChange);
            }
        });

        return Task.CompletedTask;
    }

    private void ApplyFileChanges(UnityAssetReferenceFileChange fileChange)
    {
        var document = documentSession.OpenDocument(fileChange.FilePath);
        var modifiedText = UnityAssetReferenceTextRewriter.ApplyChanges(document.Text, fileChange.Changes);

        if (modifiedText == document.Text)
        {
            return;
        }

        document.ReplaceText(modifiedText);
        document.Save();
    }
}
