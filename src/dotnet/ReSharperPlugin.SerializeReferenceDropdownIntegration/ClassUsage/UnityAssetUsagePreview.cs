using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.ClassUsage;

public sealed class UnityAssetUsageReferenceData
{
    public UnityAssetUsageReferenceData(string filePath, IReadOnlyList<int> lineNumbers)
    {
        FilePath = filePath;
        LineNumbers = lineNumbers;
    }

    public string FilePath { get; }
    public IReadOnlyList<int> LineNumbers { get; }
}

public sealed class UnityAssetUsagePreview
{
    public UnityAssetUsagePreview(string summary, IReadOnlyList<UnityAssetUsagePreviewFile> files,
        int hiddenFilesCount = 0, string message = null)
    {
        Summary = summary;
        Message = message ?? summary;
        Files = files;
        HiddenFilesCount = hiddenFilesCount;
    }

    public string Summary { get; }
    public string Message { get; }
    public IReadOnlyList<UnityAssetUsagePreviewFile> Files { get; }
    public int HiddenFilesCount { get; }
    public string FirstAssetPath => Files.Count == 0 ? null : Files[0].AssetPath;
}

public sealed class UnityAssetUsagePreviewFile
{
    public UnityAssetUsagePreviewFile(string assetPath, IReadOnlyList<int> lineNumbers)
    {
        AssetPath = assetPath;
        LineNumbers = lineNumbers;
    }

    public string AssetPath { get; }
    public IReadOnlyList<int> LineNumbers { get; }
    public int ReferencesCount => LineNumbers.Count;
}

public static class UnityAssetUsagePreviewBuilder
{
    private const int MaxFilesInPreview = 30;

    public static UnityAssetUsagePreview Build(string solutionPath,
        IReadOnlyList<UnityAssetUsageReferenceData> references)
    {
        if (references == null)
        {
            return new UnityAssetUsagePreview("Unity asset usage scan was cancelled.",
                Array.Empty<UnityAssetUsagePreviewFile>());
        }

        if (references.Count == 0)
        {
            return new UnityAssetUsagePreview("No Unity asset files reference this type.",
                Array.Empty<UnityAssetUsagePreviewFile>());
        }

        var files = references
            .Select(reference => new UnityAssetUsagePreviewFile(
                NormalizeUnityAssetPath(GetRelativePath(solutionPath, reference.FilePath)),
                reference.LineNumbers.OrderBy(lineIndex => lineIndex).ToArray()))
            .ToArray();

        var totalReferences = files.Sum(file => file.ReferencesCount);
        var previewLines = files
            .Take(MaxFilesInPreview)
            .Select(file =>
                $"{file.AssetPath}: {file.ReferencesCount} references, lines {string.Join(", ", file.LineNumbers)}")
            .ToList();

        var remainingFiles = files.Length - MaxFilesInPreview;
        if (remainingFiles > 0)
        {
            previewLines.Add($"...and {remainingFiles} more files.");
        }

        var summary = $"Found {totalReferences} references in {files.Length} Unity asset files.";
        return new UnityAssetUsagePreview(summary, files, Math.Max(remainingFiles, 0),
            $"{summary}\n\n{string.Join(Environment.NewLine, previewLines)}");
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (filePath.StartsWith(normalizedRoot))
        {
            return filePath.Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return filePath;
    }

    private static string NormalizeUnityAssetPath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
