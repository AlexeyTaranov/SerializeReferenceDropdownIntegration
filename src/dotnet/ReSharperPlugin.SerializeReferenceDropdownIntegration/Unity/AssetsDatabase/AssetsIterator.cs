using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.ProjectModel;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public static class AssetsIterator
{
    public record struct UnityReferenceTypeLineData(UnityTypeData Type, int LineIndex, bool MultiLine);

    private record struct LineInfo(string Text, int LineIndex);
    private record struct ReadTextData(LineInfo CurrentLineText, LineInfo PreviousLineText);
    private static readonly string[] unitySerializeReferenceAssetsExtensions = [".unity", ".prefab", ".asset"];

    public static readonly Regex serializeReferenceRegex = new (@"type:\s*\{class:\s*([^,]+),\s*ns:\s*([^,]+),\s*asm:\s*([^\}]+)\}");

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

    public static async Task FillReferenceTypesBlocksAsync(string path, List<UnityReferenceTypeLineData> fileTypes)
    {
        await ReadReferencesBlockInUnityAsset(path, OnReferenceLineRead);

        void OnReferenceLineRead(ReadTextData readTextData)
        {
            var currentLineText = readTextData.CurrentLineText.Text;
            var match = serializeReferenceRegex.Match(currentLineText);
            var isMultiLine = false;
            if (match.Success == false)
            {
                // Serialize reference sometimes on long names can fit on multiple lines (now i will check only two lines)
                //      type: {class: MyReallyBigClass, ns: Game.Core.Main.ReallyBigNamespaceFooBar,
                //   asm: Assembly-CSharp}
                var previousAndCurrentLine = readTextData.PreviousLineText.Text + readTextData.CurrentLineText.Text;
                match = serializeReferenceRegex.Match(previousAndCurrentLine);
                isMultiLine = true;
            }

            if (match.Success)
            {
                var className = match.Groups[1].Value;
                var ns = match.Groups[2].Value;
                var asm = match.Groups[3].Value;
                var unityTypeData = new UnityTypeData()
                {
                    ClassName = className,
                    Namespace = ns,
                    AssemblyName = asm
                };

                var lineIndex = isMultiLine
                    ? readTextData.PreviousLineText.LineIndex
                    : readTextData.CurrentLineText.LineIndex;
                fileTypes?.Add(new UnityReferenceTypeLineData(unityTypeData, lineIndex, isMultiLine));
            }
        }
    }

    private static async Task ReadReferencesBlockInUnityAsset(string path, Action<ReadTextData> onReferenceLineRead)
    {
        bool insideReferences = false;

        using var reader = new StreamReader(path);
        string line;
        int lineIndex = -1;
        var previousLine = new LineInfo(string.Empty, -1);
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

            var currentLine = new LineInfo(line, lineIndex);
            onReferenceLineRead.Invoke(new ReadTextData(currentLine, previousLine));
            
            previousLine = currentLine;
        }
    }
}