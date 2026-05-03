using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge.AsmdefRename
{
    internal sealed class AsmdefRenameScanResult
    {
        public readonly string TargetAssetPath;
        public readonly string OldName;
        public readonly string NewName;
        public readonly List<AsmdefRenameFileChange> FileChanges = new List<AsmdefRenameFileChange>();
        public readonly List<AsmdefRenameFileChange> SerializeReferenceYamlChanges = new List<AsmdefRenameFileChange>();
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

                foreach (var change in SerializeReferenceYamlChanges)
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

    internal sealed class AsmdefRenameApplyResult
    {
        public int WrittenFilesCount;
        public int WrittenReplacementsCount;
        public int WrittenYamlFilesCount;
        public int WrittenYamlReplacementsCount;
        public bool AssetFileRenamed;
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
                WillRenameAssetFile = ShouldRenameAssetFile(targetAsmdefAssetPath, newName)
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

            result.SerializeReferenceYamlChanges.AddRange(ScanSerializeReferenceYamlAssemblyChanges(oldName, newName));
            return result;
        }

        public static IReadOnlyList<AsmdefRenameFileChange> ScanSerializeReferenceYamlAssemblyChanges(
            string oldAssemblyName, string newAssemblyName)
        {
            if (string.IsNullOrWhiteSpace(oldAssemblyName))
            {
                throw new ArgumentException("Old SerializeReference assembly name is empty.", nameof(oldAssemblyName));
            }

            if (string.IsNullOrWhiteSpace(newAssemblyName))
            {
                throw new ArgumentException("New SerializeReference assembly name is empty.", nameof(newAssemblyName));
            }

            if (string.Equals(oldAssemblyName, newAssemblyName, StringComparison.Ordinal))
            {
                return Array.Empty<AsmdefRenameFileChange>();
            }

            var changes = new List<AsmdefRenameFileChange>();
            foreach (var assetPath in EnumerateUnityYamlAssetPaths())
            {
                var absolutePath = ToAbsoluteProjectPath(assetPath);
                var originalText = File.ReadAllText(absolutePath);
                var updatedText = ReplaceSerializeReferenceAssemblyLines(originalText, oldAssemblyName, newAssemblyName,
                    out var replacementsCount);
                if (replacementsCount > 0)
                {
                    changes.Add(new AsmdefRenameFileChange(assetPath, updatedText, replacementsCount));
                }
            }

            return changes;
        }

        public static AsmdefRenameApplyResult ApplySerializeReferenceYamlAssemblyChanges(
            IReadOnlyList<AsmdefRenameFileChange> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            var result = new AsmdefRenameApplyResult();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var change in changes)
                {
                    WriteAndImport(change);
                    result.WrittenYamlFilesCount++;
                    result.WrittenYamlReplacementsCount += change.ReplacementsCount;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return result;
        }

        public static AsmdefRenameApplyResult Apply(AsmdefRenameScanResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var applyResult = new AsmdefRenameApplyResult();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var change in result.FileChanges)
                {
                    WriteAndImport(change);
                    applyResult.WrittenFilesCount++;
                    applyResult.WrittenReplacementsCount += change.ReplacementsCount;
                }

                foreach (var change in result.SerializeReferenceYamlChanges)
                {
                    WriteAndImport(change);
                    applyResult.WrittenYamlFilesCount++;
                    applyResult.WrittenYamlReplacementsCount += change.ReplacementsCount;
                }

                if (result.WillRenameAssetFile)
                {
                    var renameError = AssetDatabase.RenameAsset(result.TargetAssetPath, result.NewName);
                    if (!string.IsNullOrEmpty(renameError))
                    {
                        throw new IOException($"Failed to rename asmdef asset file '{result.TargetAssetPath}': {renameError}");
                    }

                    applyResult.AssetFileRenamed = true;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return applyResult;
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

        private static IEnumerable<string> EnumerateUnityYamlAssetPaths()
        {
            foreach (var root in new[] { "Assets" })
            {
                var absoluteRoot = ToAbsoluteProjectPath(root);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var filePath in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
                {
                    var extension = Path.GetExtension(filePath);
                    if (IsUnityYamlExtension(extension))
                    {
                        yield return ToProjectRelativeAssetPath(filePath);
                    }
                }
            }
        }

        private static bool IsUnityYamlExtension(string extension)
        {
            switch (extension)
            {
                case ".asset":
                case ".controller":
                case ".mat":
                case ".overrideController":
                case ".playable":
                case ".prefab":
                case ".unity":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldRenameAssetFile(string targetAsmdefAssetPath, string newName)
        {
            return !string.Equals(Path.GetFileNameWithoutExtension(targetAsmdefAssetPath), newName,
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

        private static string ReplaceSerializeReferenceAssemblyLines(string text, string oldAssemblyName,
            string newAssemblyName, out int replacementsCount)
        {
            var count = 0;
            var pattern = @"(?<prefix>\basm:\s*)" + Regex.Escape(oldAssemblyName) + @"(?<suffix>[}\],\s\r\n])";
            var result = Regex.Replace(text, pattern, match =>
            {
                count++;
                return match.Groups["prefix"].Value + newAssemblyName + match.Groups["suffix"].Value;
            });
            replacementsCount = count;
            return result;
        }

        private static void WriteAndImport(AsmdefRenameFileChange change)
        {
            var absolutePath = ToAbsoluteProjectPath(change.AssetPath);
            File.WriteAllText(absolutePath, change.NewText);
            var writtenText = File.ReadAllText(absolutePath);
            if (!string.Equals(writtenText, change.NewText, StringComparison.Ordinal))
            {
                throw new IOException($"Failed to verify written asset change: {change.AssetPath}");
            }

            AssetDatabase.ImportAsset(change.AssetPath, ImportAssetOptions.ForceUpdate);
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
