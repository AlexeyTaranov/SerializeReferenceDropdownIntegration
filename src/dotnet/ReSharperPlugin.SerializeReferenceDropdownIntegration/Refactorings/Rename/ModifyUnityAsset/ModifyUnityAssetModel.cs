using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetModel
{
    private const int MaxPreviewFiles = 10;

    private readonly UnityTypeData oldType;
    private readonly UnityTypeData newType;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly UnityAssetReferenceDocumentWriter documentWriter;
    private readonly PluginDiagnostics diagnostics;
    private readonly List<UnityAssetReferenceScanner.TypeReferenceData> targetTypeData = new();
    private readonly List<FilePreviewData> previewData = new();
    private bool previewLoaded;

    public IReadOnlyList<UnityAssetReferenceChange> PreviewChanges =>
        previewData.SelectMany(data => data.Changes).ToArray();

    public int PreviewFilesCount => previewData.Count(data => data.Changes.Count > 0);
    public bool ShouldApplyModifiedFiles { get; set; }

    private readonly record struct FilePreviewData(string FilePath, IReadOnlyList<UnityAssetReferenceChange> Changes);

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, UnityAssetReferenceScanner scanner,
        UnityAssetReferenceDocumentWriter documentWriter, PluginDiagnostics diagnostics)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.scanner = scanner;
        this.documentWriter = documentWriter;
        this.diagnostics = diagnostics;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        targetTypeData.Clear();
        previewData.Clear();
        previewLoaded = false;
        var collectedTypeData = await scanner.CollectTypeReferencesAsync(oldType, cancellationToken);
        if (collectedTypeData == null)
        {
            return -1;
        }

        targetTypeData.AddRange(collectedTypeData);

        foreach (var data in targetTypeData)
        {
            var changes = UnityAssetReferenceRewriter.PreviewChanges(
                File.ReadAllLines(data.FilePath),
                data.References,
                data.PrefabOverrides,
                newType);

            if (changes.Count > 0)
            {
                previewData.Add(new FilePreviewData(data.FilePath, changes));
            }
        }

        previewLoaded = true;
        return PreviewChanges.Count;
    }

    public string BuildPreviewText()
    {
        if (!previewLoaded)
        {
            return "Affected files will appear here after scanning.";
        }

        if (previewData.Count == 0)
        {
            return "Affected files: none";
        }

        var previewLines = new List<string> { "Affected files:" };

        foreach (var fileData in previewData.Take(MaxPreviewFiles))
        {
            previewLines.Add(Path.GetFileName(fileData.FilePath));
        }

        var remainingFiles = previewData.Count - MaxPreviewFiles;
        if (remainingFiles > 0)
        {
            previewLines.Add($"...and {remainingFiles} more files");
        }

        return string.Join(Environment.NewLine, previewLines);
    }

    public async Task ModifyAllFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyModifiedFiles)
        {
            return;
        }

        if (!previewLoaded)
        {
            await FetchSerializeReferenceCountInAssetsFolderAsync(cancellationToken);
        }

        var fileChanges = previewData
            .Select(data => new UnityAssetReferenceFileChange(data.FilePath, data.Changes))
            .ToArray();

        await documentWriter.ApplyChangesAsync(fileChanges);
    }

    public void LogModificationFailure(Exception exception)
    {
        diagnostics.Error($"Failed to modify Unity assets for '{oldType.GetFullTypeName()}'.", exception);
    }

}
