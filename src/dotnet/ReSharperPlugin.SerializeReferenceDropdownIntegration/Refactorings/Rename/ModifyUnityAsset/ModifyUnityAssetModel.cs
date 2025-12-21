using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetModel
{
    private readonly UnityTypeData oldType;
    private readonly UnityTypeData newType;
    private readonly ISolution solution;
    private readonly List<TypeReferenceData> targetTypeData = new();

    private record TypeReferenceData(
        string FilePath,
        IReadOnlyList<AssetsIterator.UnityReferenceTypeLineData> References,
        IReadOnlyList<AssetsIterator.UnityReferenceTypePrefabOverrideLineData> PrefabOverrides);

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, ISolution solution)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.solution = solution;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        var allUnityFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);

        var totalCount = 0;
        targetTypeData.Clear();

        var allReferences = new List<AssetsIterator.UnityReferenceTypeLineData>();
        var allPrefabOverrides = new List<AssetsIterator.UnityReferenceTypePrefabOverrideLineData>();

        foreach (var filePath in allUnityFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }

            allReferences.Clear();
            allPrefabOverrides.Clear();

            await AssetsIterator.FillReferenceTypesBlocksAsync(filePath, allReferences, allPrefabOverrides);

            var targetReferences = allReferences.Where(t => t.Type == oldType).ToArray();
            var targetPrefabOverrides = allPrefabOverrides.Where(t => t.Type == oldType).ToArray();
            if (targetReferences.Any() || targetPrefabOverrides.Any())
            {
                targetTypeData.Add(new TypeReferenceData(filePath, targetReferences, targetPrefabOverrides));
                totalCount += targetReferences.Length + targetPrefabOverrides.Length;
            }
        }

        return totalCount;
    }

    public async Task ModifyAllFilesAsync()
    {
        var allLines = new List<string>();
        foreach (var data in targetTypeData)
        {
            allLines.Clear();
            allLines.AddRange(File.ReadAllLines(data.FilePath));

            foreach (var prefabOverride in data.PrefabOverrides)
            {
                var oldLine = allLines[prefabOverride.LineIndex];

                var fullTypeName = string.IsNullOrEmpty(newType.Namespace)
                    ? newType.ClassName
                    : $"{newType.AssemblyName}.{newType.ClassName}";

                var newLine = AssetsIterator.prefabOverrideSerializeReferenceTypeRegex.Replace(
                    oldLine,
                    $"value: {newType.AssemblyName} {fullTypeName}"
                );

                allLines[prefabOverride.LineIndex] = newLine;
            }

            foreach (var modifyTypeData in data.References)
            {
                var oldLineText = allLines[modifyTypeData.LineIndex];
                if (modifyTypeData.MultiLine)
                {
                    var nextLineIndex = modifyTypeData.LineIndex + 1;
                    oldLineText += allLines[nextLineIndex];
                    allLines.RemoveAt(nextLineIndex);
                }

                var newLine = AssetsIterator.serializeReferenceRegex.Replace(oldLineText,
                    $"type: {{class: {newType.ClassName}, ns: {newType.Namespace}, asm: {newType.AssemblyName}}}");

                allLines[modifyTypeData.LineIndex] = newLine;
            }


            File.WriteAllLines(data.FilePath, allLines);
        }
    }
}