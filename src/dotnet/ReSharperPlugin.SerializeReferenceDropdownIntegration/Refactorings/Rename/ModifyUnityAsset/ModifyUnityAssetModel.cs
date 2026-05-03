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

    private readonly Func<UnityTypeData, bool> isTargetType;
    private readonly Func<UnityTypeData, UnityTypeData?> tryMapType;
    private readonly string diagnosticTarget;
    private readonly UnityAssetReferenceScanner scanner;
    private readonly UnityAssetReferenceDocumentWriter documentWriter;
    private readonly PluginDiagnostics diagnostics;
    private readonly List<UnityAssetReferenceScanner.TypeReferenceData> targetTypeData = new();
    private readonly List<FilePreviewData> previewData = new();
    private readonly List<UnityAssetReferenceScanner.ScanWarning> scanWarnings = new();
    private bool previewLoaded;

    public IReadOnlyList<UnityAssetReferenceChange> PreviewChanges =>
        previewData.SelectMany(data => data.Changes).ToArray();

    public int PreviewFilesCount => previewData.Count(data => data.Changes.Count > 0);
    public IReadOnlyList<UnityAssetReferenceScanner.ScanWarning> ScanWarnings => scanWarnings;
    public string LastScanStatusText { get; private set; } = "Scan has not run yet.";
    public bool ShouldApplyModifiedFiles { get; set; }

    private readonly record struct FilePreviewData(string FilePath, IReadOnlyList<UnityAssetReferenceChange> Changes);

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, UnityAssetReferenceScanner scanner,
        UnityAssetReferenceDocumentWriter documentWriter, PluginDiagnostics diagnostics)
        : this(
            type => type == oldType,
            type => type == oldType ? newType : null,
            oldType.GetFullTypeName(),
            scanner,
            documentWriter,
            diagnostics)
    {
    }

    public static ModifyUnityAssetModel CreateNamespaceRenameModel(string oldNamespace, string newNamespace,
        UnityAssetReferenceScanner scanner, UnityAssetReferenceDocumentWriter documentWriter,
        PluginDiagnostics diagnostics)
    {
        return new ModifyUnityAssetModel(
            type => IsSameOrNestedNamespace(type.Namespace, oldNamespace),
            type => IsSameOrNestedNamespace(type.Namespace, oldNamespace)
                ? type with { Namespace = RenameNamespacePrefix(type.Namespace, oldNamespace, newNamespace) }
                : null,
            oldNamespace,
            scanner,
            documentWriter,
            diagnostics);
    }

    private static bool IsSameOrNestedNamespace(string currentNamespace, string oldNamespace)
    {
        return currentNamespace == oldNamespace ||
               currentNamespace.StartsWith(oldNamespace + ".", StringComparison.Ordinal);
    }

    private static string RenameNamespacePrefix(string currentNamespace, string oldNamespace, string newNamespace)
    {
        if (currentNamespace == oldNamespace)
        {
            return newNamespace;
        }

        return newNamespace + currentNamespace.Substring(oldNamespace.Length);
    }

    private ModifyUnityAssetModel(Func<UnityTypeData, bool> isTargetType,
        Func<UnityTypeData, UnityTypeData?> tryMapType, string diagnosticTarget,
        UnityAssetReferenceScanner scanner, UnityAssetReferenceDocumentWriter documentWriter,
        PluginDiagnostics diagnostics)
    {
        this.isTargetType = isTargetType;
        this.tryMapType = tryMapType;
        this.diagnosticTarget = diagnosticTarget;
        this.scanner = scanner;
        this.documentWriter = documentWriter;
        this.diagnostics = diagnostics;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        targetTypeData.Clear();
        previewData.Clear();
        scanWarnings.Clear();
        previewLoaded = false;
        LastScanStatusText = "Scanning Unity assets...";
        var scanResult = await scanner.CollectTypeReferencesWithDiagnosticsAsync(isTargetType, diagnosticTarget,
            cancellationToken);
        if (scanResult.Cancelled)
        {
            LastScanStatusText = "Unity asset scan was cancelled.";
            return -1;
        }

        scanWarnings.AddRange(scanResult.Warnings);
        targetTypeData.AddRange(scanResult.References);

        foreach (var data in targetTypeData)
        {
            var changes = UnityAssetReferenceRewriter.PreviewChanges(
                File.ReadAllLines(data.FilePath),
                data.References,
                data.PrefabOverrides,
                tryMapType);

            if (changes.Count > 0)
            {
                previewData.Add(new FilePreviewData(data.FilePath, changes));
            }
        }

        previewLoaded = true;
        LastScanStatusText = BuildScanStatusText(scanResult);
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
        diagnostics.Error($"Failed to modify Unity assets for '{diagnosticTarget}'.", exception);
    }

    private string BuildScanStatusText(UnityAssetReferenceScanner.TypeReferencesScanResult scanResult)
    {
        if (!scanResult.AssetsFolderFound)
        {
            return "Unity Assets folder was not found. Open a Unity project or check the solution root.";
        }

        if (scanResult.TotalFiles == 0)
        {
            return "No Unity YAML asset files were found under the Assets folder.";
        }

        if (scanWarnings.Count > 0 && previewData.Count == 0)
        {
            return $"No Unity YAML files need to be changed. {scanWarnings.Count} asset {Pluralize(scanWarnings.Count, "file was", "files were")} skipped.";
        }

        if (scanWarnings.Count > 0)
        {
            return $"{scanWarnings.Count} asset {Pluralize(scanWarnings.Count, "file was", "files were")} skipped during scan.";
        }

        return previewData.Count == 0
            ? "No Unity YAML files need to be changed."
            : string.Empty;
    }

    private static string Pluralize(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}
