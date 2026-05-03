using System;
using SerializeReferenceDropdownBridge.Bridge;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge.AsmdefRename
{
    internal sealed class AsmdefRenameWindow : EditorWindow
    {
        private string asmdefAssetPath;
        private string oldName;
        private string newName;
        private AsmdefRenameScanResult scanResult;
        private int selectedAction;
        private string statusText = "Scan has not run yet.";

        public static void Open(AsmdefRenameRequest request)
        {
            var window = GetWindow<AsmdefRenameWindow>(true, "Rename Assembly Definition");
            window.minSize = new Vector2(420, 210);
            window.Initialize(request);
            window.ShowUtility();
        }

        private void Initialize(AsmdefRenameRequest request)
        {
            asmdefAssetPath = request?.asmdefPath;
            if (string.IsNullOrWhiteSpace(asmdefAssetPath))
            {
                asmdefAssetPath = AsmdefRenameService.GetSelectedAsmdefAssetPath();
            }

            oldName = !string.IsNullOrWhiteSpace(request?.oldName)
                ? request.oldName
                : AsmdefRenameService.ReadAssemblyName(asmdefAssetPath);
            newName = !string.IsNullOrWhiteSpace(request?.newName) ? request.newName : oldName;
            scanResult = null;
            selectedAction = 0;
            statusText = "Scan has not run yet.";
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Modify Assembly Definition", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool renames the asmdef, updates asmdef/asmref references, and replaces matching SerializeReference asm values in Unity YAML assets.",
                MessageType.Info);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Asset", asmdefAssetPath ?? string.Empty);
                EditorGUILayout.TextField("Current name", oldName ?? string.Empty);
            }

            EditorGUI.BeginChangeCheck();
            newName = EditorGUILayout.TextField("New name", newName ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                scanResult = null;
                selectedAction = 0;
                statusText = "Scan has not run yet.";
            }

            EditorGUILayout.Space();
            if (GUILayout.Button(scanResult == null ? "Scan" : "Rescan"))
            {
                Scan();
            }

            EditorGUILayout.HelpBox(statusText, MessageType.Info);

            using (new EditorGUI.DisabledScope(scanResult == null || scanResult.ChangesCount == 0))
            {
                selectedAction = GUILayout.SelectionGrid(selectedAction,
                    new[] { "No changes", "Write asmdef and YAML changes to disk" }, 1);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(scanResult == null || selectedAction == 0 || scanResult.ChangesCount == 0))
            {
                if (GUILayout.Button("Apply"))
                {
                    Apply();
                }
            }
        }

        private void Scan()
        {
            try
            {
                scanResult = AsmdefRenameService.Scan(asmdefAssetPath, oldName, newName);
                statusText = scanResult.ChangesCount == 0
                    ? "No asmdef, asmref, or SerializeReference YAML files need to be changed."
                    : $"{scanResult.ChangesCount} change(s): {scanResult.FileChanges.Count} asmdef/asmref file(s), " +
                      $"{scanResult.SerializeReferenceYamlChanges.Count} YAML asset file(s)." +
                      (scanResult.WillRenameAssetFile ? " The target .asmdef file will also be renamed." : string.Empty);
                selectedAction = 0;
            }
            catch (Exception exception)
            {
                scanResult = null;
                selectedAction = 0;
                statusText = exception.Message;
                Log.Error(exception);
            }
        }

        private void Apply()
        {
            try
            {
                var result = AsmdefRenameService.Apply(scanResult);
                statusText = $"Applied {result.WrittenReplacementsCount} asmdef/asmref replacement(s) in {result.WrittenFilesCount} file(s), " +
                             $"{result.WrittenYamlReplacementsCount} SerializeReference asm replacement(s) in {result.WrittenYamlFilesCount} YAML file(s)." +
                             (result.AssetFileRenamed ? " The target .asmdef asset was renamed." : string.Empty);
                oldName = newName;
                scanResult = null;
                selectedAction = 0;
            }
            catch (Exception exception)
            {
                statusText = exception.Message;
                Log.Error(exception);
            }
        }
    }
}
