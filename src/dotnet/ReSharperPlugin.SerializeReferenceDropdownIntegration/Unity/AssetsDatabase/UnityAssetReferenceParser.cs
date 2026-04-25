using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

public readonly record struct UnityReferenceTypeLineData(UnityTypeData Type, int LineIndex, bool MultiLine);

public readonly record struct UnityReferenceTypePrefabOverrideLineData(UnityTypeData Type, int LineIndex);

public static class UnityAssetReferenceParser
{
    private readonly record struct LineInfo(string Text, int LineIndex);

    private readonly record struct ReadTextData(LineInfo CurrentLineText, LineInfo PreviousLineText);

    public static readonly Regex SerializeReferenceRegex =
        new(@"type:\s*\{class:\s*([^,]+),\s*ns:\s*([^,]+),\s*asm:\s*([^\}]+)\}");

    public static readonly Regex PrefabOverrideSerializeReferenceTypeRegex =
        new(@"value:\s+([^\s]+)\s+(.+)\.([^.]+)", RegexOptions.Compiled);

    public static async Task FillReferenceTypesBlocksAsync(string path, List<UnityReferenceTypeLineData> referenceTypes,
        List<UnityReferenceTypePrefabOverrideLineData> prefabOverrides)
    {
        await ReadReferencesBlockInUnityAsset(path, OnReferenceLineRead, OnPrefabOverrideTypeRead);

        void OnReferenceLineRead(ReadTextData readTextData)
        {
            var currentLineText = readTextData.CurrentLineText.Text;
            var match = SerializeReferenceRegex.Match(currentLineText);
            var isMultiLine = false;
            if (match.Success == false)
            {
                if (readTextData.PreviousLineText.Text.Contains("type:") && currentLineText.Contains("asm:"))
                {
                    // Unity can wrap long SerializeReference type names across two lines.
                    var previousAndCurrentLine = readTextData.PreviousLineText.Text + readTextData.CurrentLineText.Text;
                    match = SerializeReferenceRegex.Match(previousAndCurrentLine);
                    isMultiLine = true;
                }
            }

            if (match.Success)
            {
                var unityTypeData = new UnityTypeData
                {
                    ClassName = match.Groups[1].Value,
                    Namespace = match.Groups[2].Value,
                    AssemblyName = match.Groups[3].Value
                };

                var lineIndex = isMultiLine
                    ? readTextData.PreviousLineText.LineIndex
                    : readTextData.CurrentLineText.LineIndex;
                referenceTypes.Add(new UnityReferenceTypeLineData(unityTypeData, lineIndex, isMultiLine));
            }
        }

        void OnPrefabOverrideTypeRead(UnityTypeData prefabOverride, int lineIndex)
        {
            prefabOverrides.Add(new UnityReferenceTypePrefabOverrideLineData(prefabOverride, lineIndex));
        }
    }

    private static async Task ReadReferencesBlockInUnityAsset(string path,
        Action<ReadTextData> onReferenceLineRead, Action<UnityTypeData, int> onPrefabOverrideTypeRead)
    {
        var insideReferences = false;

        using var reader = new StreamReader(path);
        string line;
        var lineIndex = -1;
        var previousLine = new LineInfo(string.Empty, -1);
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
        {
            lineIndex++;

            if (previousLine.Text?.TrimStart().StartsWith("propertyPath: 'managedReferences[") == true)
            {
                if (TryParsePrefabOverrideTypeFromString(line, out var overrideUnityType))
                {
                    onPrefabOverrideTypeRead.Invoke(overrideUnityType, lineIndex);
                }
            }

            line = line.TrimStart();
            var currentLine = new LineInfo(line, lineIndex);

            if (line.StartsWith("---"))
            {
                insideReferences = false;
                previousLine = currentLine;
                continue;
            }

            if (!insideReferences && line.StartsWith("references:"))
            {
                insideReferences = true;
                previousLine = currentLine;
                continue;
            }

            if (!insideReferences)
            {
                previousLine = currentLine;
                continue;
            }

            onReferenceLineRead.Invoke(new ReadTextData(currentLine, previousLine));

            previousLine = currentLine;
        }
    }

    private static bool TryParsePrefabOverrideTypeFromString(string input, out UnityTypeData unityType)
    {
        unityType = default;

        var match = PrefabOverrideSerializeReferenceTypeRegex.Match(input);
        if (match.Success == false)
        {
            return false;
        }

        unityType = new UnityTypeData(
            match.Groups[3].Value,
            match.Groups[2].Value,
            match.Groups[1].Value);

        return true;
    }
}
