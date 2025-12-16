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

    public ModifyUnityAssetModel(UnityTypeData oldType, UnityTypeData newType, ISolution solution)
    {
        this.oldType = oldType;
        this.newType = newType;
        this.solution = solution;
    }

    public async Task<int> FetchSerializeReferenceCountInAssetsFolderAsync(CancellationToken cancellationToken)
    {
        var allUnityFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);
        var oldTypeStr = oldType.BuildSerializeReferenceTypeString();

        int totalCount = 0;

        foreach (var filePath in allUnityFiles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return -1;
            }
            
            var referenceCount = await AssetsIterator.CountInAllReferencesBlocksAsync(filePath, oldTypeStr);
            totalCount += referenceCount;
        }

        return totalCount;
    }

    public async Task ModifyAllFilesAsync()
    {
        var allFiles = AssetsIterator.GetUnityFilesInAssetsFolder(solution);
        
        var modifyLines = new List<int>();
        
        var oldTypeString = oldType.BuildSerializeReferenceTypeString();
        var newTypeString = newType.BuildSerializeReferenceTypeString();
        
        foreach (var filePath in allFiles)
        {
            modifyLines.Clear();
            await ModifyReferenceBlock(filePath);
        }

        async Task ModifyReferenceBlock(string filePath)
        {
            await AssetsIterator.CountInAllReferencesBlocksAsync(filePath, oldTypeString, modifyLines);
            if (modifyLines.Any() == false)
            {
                return;
            }

            var allLines = File.ReadAllLines(filePath);
            foreach (var modifyLine in modifyLines)
            {
                allLines[modifyLine] = allLines[modifyLine].Replace(oldTypeString, newTypeString);
            }

            File.WriteAllLines(filePath, allLines);
        }
    }


}