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
    private readonly Dictionary<string, IReadOnlyList<AssetsIterator.UnityReferenceTypeLineData>> targetPathTypes = new(); 

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
        targetPathTypes.Clear();

        var allTypes = new List<AssetsIterator.UnityReferenceTypeLineData>();

        foreach (var filePath in allUnityFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }

            allTypes.Clear();
            await AssetsIterator.FillReferenceTypesBlocksAsync(filePath, allTypes);
            var targetTypes = allTypes.Where(t => t.Type == oldType).ToArray();
            if (targetTypes.Any())
            {
                targetPathTypes[filePath] = targetTypes;
                totalCount += targetTypes.Length;
            }
        }

        return totalCount;
    }

    public async Task ModifyAllFilesAsync()
    {
        var allLines = new List<string>();
        foreach (var targetTypePath in targetPathTypes)
        {
            var filePath = targetTypePath.Key;
            allLines.Clear();
            allLines.AddRange(File.ReadAllLines(filePath));
            foreach (var modifyTypeData in targetTypePath.Value)
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
            
            File.WriteAllLines(filePath, allLines);
        }
    }
}