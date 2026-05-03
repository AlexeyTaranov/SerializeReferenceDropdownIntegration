using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge
{
    internal sealed class AsmdefRenameScanResult
    {
        public readonly string TargetAssetPath;
        public readonly string OldName;
        public readonly string NewName;
        public readonly List<AsmdefRenameFileChange> FileChanges = new List<AsmdefRenameFileChange>();
        public bool WillRenameAssetFile;

        public AsmdefRenameScanResult(string targetAssetPath, string oldName, string newName)
        {
            TargetAssetPath = targetAssetPath;
            OldName = oldName;
            NewName = newName;
        }

        public int ChangesCount
        {
            get
            {
                var count = WillRenameAssetFile ? 1 : 0;
                foreach (var change in FileChanges)
                {
                    count += change.ReplacementsCount;
                }

                return count;
            }
        }
    }

    internal sealed class AsmdefRenameFileChange
    {
        public readonly string AssetPath;
        public readonly string NewText;
        public readonly int ReplacementsCount;

        public AsmdefRenameFileChange(string assetPath, string newText, int replacementsCount)
        {
            AssetPath = assetPath;
            NewText = newText;
            ReplacementsCount = replacementsCount;
        }
    }

    internal static class AsmdefRenameService
    {
        private static readonly Regex NamePropertyRegex =
            new Regex("(\"name\"\\s*:\\s*\")(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled);

        public static bool IsAsmdefAssetPath(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath) &&
                   assetPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetSelectedAsmdefAssetPath()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            return IsAsmdefAssetPath(assetPath) ? assetPath : null;
        }

        public static string ReadAssemblyName(string asmdefAssetPath)
        {
            if (!IsAsmdefAssetPath(asmdefAssetPath))
            {
                return null;
            }

            var absolutePath = ToAbsoluteProjectPath(asmdefAssetPath);
            if (!File.Exists(absolutePath))
            {
                return null;
            }

            var match = NamePropertyRegex.Match(File.ReadAllText(absolutePath));
            return match.Success ? UnescapeJsonString(match.Groups["value"].Value) : null;
        }

        public static AsmdefRenameScanResult Scan(string targetAsmdefAssetPath, string oldName, string newName)
        {
            if (!IsAsmdefAssetPath(targetAsmdefAssetPath))
            {
                throw new ArgumentException("Target asset is not an .asmdef file.", nameof(targetAsmdefAssetPath));
            }

            if (string.IsNullOrWhiteSpace(oldName))
            {
                oldName = ReadAssemblyName(targetAsmdefAssetPath);
            }

            if (string.IsNullOrWhiteSpace(oldName))
            {
                throw new InvalidOperationException("Could not read the current assembly definition name.");
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("New assembly definition name is empty.", nameof(newName));
            }

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                return new AsmdefRenameScanResult(targetAsmdefAssetPath, oldName, newName);
            }

            var result = new AsmdefRenameScanResult(targetAsmdefAssetPath, oldName, newName)
            {
                WillRenameAssetFile = ShouldRenameAssetFile(targetAsmdefAssetPath, oldName)
            };

            foreach (var assetPath in EnumerateAsmdefAndAsmrefAssetPaths())
            {
                var absolutePath = ToAbsoluteProjectPath(assetPath);
                var originalText = File.ReadAllText(absolutePath);
                var updatedText = ReplaceExactJsonStringValues(originalText, oldName, newName, out var replacementsCount);
                if (replacementsCount > 0)
                {
                    result.FileChanges.Add(new AsmdefRenameFileChange(assetPath, updatedText, replacementsCount));
                }
            }

            return result;
        }

        public static void Apply(AsmdefRenameScanResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var change in result.FileChanges)
                {
                    File.WriteAllText(ToAbsoluteProjectPath(change.AssetPath), change.NewText);
                }

                if (result.WillRenameAssetFile)
                {
                    var renameError = AssetDatabase.RenameAsset(result.TargetAssetPath, result.NewName);
                    if (!string.IsNullOrEmpty(renameError))
                    {
                        Log.DevWarning($"Failed to rename asmdef asset file '{result.TargetAssetPath}': {renameError}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

        private static IEnumerable<string> EnumerateAsmdefAndAsmrefAssetPaths()
        {
            foreach (var root in new[] { "Assets", "Packages" })
            {
                var absoluteRoot = ToAbsoluteProjectPath(root);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var filePath in Directory.GetFiles(absoluteRoot, "*.asmdef", SearchOption.AllDirectories))
                {
                    yield return ToProjectRelativeAssetPath(filePath);
                }

                foreach (var filePath in Directory.GetFiles(absoluteRoot, "*.asmref", SearchOption.AllDirectories))
                {
                    yield return ToProjectRelativeAssetPath(filePath);
                }
            }
        }

        private static bool ShouldRenameAssetFile(string targetAsmdefAssetPath, string oldName)
        {
            return string.Equals(Path.GetFileNameWithoutExtension(targetAsmdefAssetPath), oldName,
                StringComparison.Ordinal);
        }

        private static string ReplaceExactJsonStringValues(string text, string oldValue, string newValue,
            out int replacementsCount)
        {
            var count = 0;
            var oldJsonValue = "\"" + EscapeJsonString(oldValue) + "\"";
            var newJsonValue = "\"" + EscapeJsonString(newValue) + "\"";
            var result = Regex.Replace(text, Regex.Escape(oldJsonValue), match =>
            {
                count++;
                return newJsonValue;
            });
            replacementsCount = count;
            return result;
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToProjectRelativeAssetPath(string absolutePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var relativePath = absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relativePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string EscapeJsonString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeJsonString(string value)
        {
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
