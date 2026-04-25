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
    private const int MaxPreviewChanges = 5;

    private readonly UnityTypeData oldType;
    private readonly UnityTypeData newType;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly PluginDiagnostics diagnostics;
    private readonly List<UnityAssetReferenceScanner.TypeReferenceData> targetTypeData = new();
    private readonly List<FilePreviewData> previewData = new();

    public IReadOnlyList<UnityAssetReferenceChange> PreviewChanges =>
        previewData.SelectMany(data => data.Changes).ToArray();

    public int PreviewFilesCount => previewData.Count(data => data.Changes.Count > 0);

    private readonly record struct FilePreviewData(string FilePath, IReadOnlyList<UnityAssetReferenceChange> Changes);

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, UnityAssetReferenceScanner scanner,
        PluginDiagnostics diagnostics)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.scanner = scanner;
        this.diagnostics = diagnostics;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        targetTypeData.Clear();
        previewData.Clear();
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

        return PreviewChanges.Count;
    }

    public string BuildPreviewText()
    {
        if (previewData.Count == 0)
        {
            return "Preview: no changes";
        }

        var previewLines = new List<string> { "Preview:" };
        var shownChanges = 0;

        foreach (var fileData in previewData)
        {
            foreach (var change in fileData.Changes)
            {
                if (shownChanges >= MaxPreviewChanges)
                {
                    var remainingChanges = PreviewChanges.Count - shownChanges;
                    previewLines.Add($"...and {remainingChanges} more changes");
                    return string.Join(Environment.NewLine, previewLines);
                }

                previewLines.Add($"{Path.GetFileName(fileData.FilePath)}:{change.LineIndex + 1}");
                previewLines.Add($"- {FormatPreviewLines(change.OldLines)}");
                previewLines.Add($"+ {FormatPreviewLines(change.NewLines)}");
                shownChanges++;
            }
        }

        return string.Join(Environment.NewLine, previewLines);
    }

    public Task ModifyAllFilesAsync()
    {
        foreach (var data in previewData)
        {
            var modifiedLines = UnityAssetReferenceRewriter.ApplyChanges(File.ReadAllLines(data.FilePath), data.Changes);
            File.WriteAllLines(data.FilePath, modifiedLines);
        }

        return Task.CompletedTask;
    }

    public void LogModificationFailure(Exception exception)
    {
        diagnostics.Error($"Failed to modify Unity assets for '{oldType.GetFullTypeName()}'.", exception);
    }

    private static string FormatPreviewLines(IReadOnlyList<string> lines)
    {
        return string.Join(" ", lines.Select(line => line.Trim()));
    }
}
