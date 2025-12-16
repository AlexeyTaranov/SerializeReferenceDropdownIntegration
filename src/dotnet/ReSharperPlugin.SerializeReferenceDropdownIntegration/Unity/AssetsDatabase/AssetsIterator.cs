using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.ProjectModel;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public static class AssetsIterator
{
    public record struct LineInfo(string Text, int LineIndex);

    private static readonly string[] unitySerializeReferenceAssetsExtensions = [".unity", ".prefab"];

    public static IReadOnlyList<string> GetUnityFilesInAssetsFolder(ISolution solution)
    {
        var unityAssetsPath = Path.Combine(solution.SolutionDirectory.FullPath, "Assets");
        var allUnityFiles = Directory.EnumerateFiles(
                unityAssetsPath,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => unitySerializeReferenceAssetsExtensions.Any(path.EndsWith)).ToArray();
        return allUnityFiles;
    }

    public static async Task<int> CountInAllReferencesBlocksAsync(string path, string search,
        List<int> targetLines = null)
    {
        int totalCount = 0;
        await ReadReferencesBlockInUnityAsset(path, CheckLineCompare);
        return totalCount;

        void CheckLineCompare(LineInfo lineData)
        {
            var contains = lineData.Text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            if (contains)
            {
                targetLines?.Add(lineData.LineIndex);
                totalCount++;
            }
        }
    }

    public static async Task ReadReferencesBlockInUnityAsset(string path, Action<LineInfo> onReferenceLineRead = null)
    {
        bool insideReferences = false;

        using var reader = new StreamReader(path);
        string line;
        int lineIndex = -1;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            lineIndex++;
            line = line.TrimStart();

            if (line.StartsWith("---"))
            {
                insideReferences = false;
                continue;
            }

            if (!insideReferences && line.StartsWith("references:"))
            {
                insideReferences = true;
                continue;
            }

            if (!insideReferences)
            {
                continue;
            }

            onReferenceLineRead?.Invoke(new LineInfo(line, lineIndex));
        }
    }
}