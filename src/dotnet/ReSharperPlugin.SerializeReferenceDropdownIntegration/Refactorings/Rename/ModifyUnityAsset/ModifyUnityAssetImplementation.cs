using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetImplementation
{
    private readonly UnityTypeData oldType;
    private readonly UnityTypeData newType;
    private readonly ISolution solution;

    public ModifyUnityAssetImplementation(UnityTypeData oldType, UnityTypeData newType, ISolution solution)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.solution = solution;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        var allUnityFiles = GetUnityFilesInAssetsFolder();
        var oldTypeStr = oldType.BuildSerializeReferenceTypeString();

        int totalCount = 0;

        foreach (var filePath in allUnityFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }

            var referenceCount = await CountInAllReferencesBlocksAsync(filePath, oldTypeStr);
            totalCount += referenceCount;
        }

        return totalCount;
    }

    public async Task ModifyAllFilesAsync()
    {
        var allFiles = GetUnityFilesInAssetsFolder();
        
        var modifyLines = new List<int>();
        
        var oldTypeString = oldType.BuildSerializeReferenceTypeString();
        var newTypeString = newType.BuildSerializeReferenceTypeString();
        
        foreach (var filePath in allFiles)
        {
            modifyLines.Clear();
            await CountInAllReferencesBlocksAsync(filePath, oldTypeString, modifyLines);
            if (modifyLines.Any() == false)
            {
                continue;
            }

            var allLines = File.ReadAllLines(filePath);
            foreach (var modifyLine in modifyLines)
            {
                allLines[modifyLine] = allLines[modifyLine].Replace(oldTypeString, newTypeString);
            }

            File.WriteAllLines(filePath, allLines);
        }
    }

    private IReadOnlyList<string> GetUnityFilesInAssetsFolder()
    {
        var unityAssetsPath = Path.Combine(solution.SolutionDirectory.FullPath, "Assets");
        var allUnityFiles = Directory.EnumerateFiles(
                unityAssetsPath,
                "*.*",
                SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".unity") || f.EndsWith(".prefab")).ToArray();
        return allUnityFiles;
    }

    private async Task<int> CountInAllReferencesBlocksAsync(string path, string search,
        List<int> targetLines = null)
    {
        int totalCount = 0;
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

            int index = 0;
            while ((index = line.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                totalCount++;
                targetLines?.Add(lineIndex);
                index += search.Length;
            }
        }

        return totalCount;
    }
}