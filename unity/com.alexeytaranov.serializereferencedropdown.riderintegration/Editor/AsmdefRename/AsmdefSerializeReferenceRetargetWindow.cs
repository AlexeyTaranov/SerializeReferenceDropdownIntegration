using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge
{
    internal sealed class AsmdefSerializeReferenceRetargetWindow : EditorWindow
    {
        private const string DefaultSourceAssembly = "Assembly-CSharp";

        private string targetAsmdefAssetPath;
        private string targetAssemblyName;
        private string sourceAssemblyName = DefaultSourceAssembly;
        private IReadOnlyList<AsmdefRenameFileChange> scanResult;
        private int selectedAction;
        private string statusText = "Scan has not run yet.";

        public static void Open(string asmdefAssetPath)
        {
            var window = GetWindow<AsmdefSerializeReferenceRetargetWindow>(true,
                "Retarget SerializeReference Assembly");
            window.minSize = new Vector2(430, 190);
            window.Initialize(asmdefAssetPath);
            window.ShowUtility();
        }

        private void Initialize(string asmdefAssetPath)
        {
            targetAsmdefAssetPath = asmdefAssetPath;
            targetAssemblyName = AsmdefRenameService.ReadAssemblyName(targetAsmdefAssetPath);
            sourceAssemblyName = DefaultSourceAssembly;
            scanResult = null;
            selectedAction = 0;
            statusText = "Scan has not run yet.";
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Rename SerializeReference To This Asmdef", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Target asmdef", targetAssemblyName ?? string.Empty);
            }

            EditorGUI.BeginChangeCheck();
            sourceAssemblyName = EditorGUILayout.TextField("From asm", sourceAssemblyName ?? string.Empty);
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

            using (new EditorGUI.DisabledScope(scanResult == null || CountChanges(scanResult) == 0))
            {
                selectedAction = GUILayout.SelectionGrid(selectedAction,
                    new[] { "No changes", "Write SerializeReference YAML changes to disk" }, 1);
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(scanResult == null || selectedAction == 0 || CountChanges(scanResult) == 0))
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
                scanResult = AsmdefRenameService.ScanSerializeReferenceYamlAssemblyChanges(sourceAssemblyName,
                    targetAssemblyName);
                var changesCount = CountChanges(scanResult);
                statusText = changesCount == 0
                    ? "No SerializeReference YAML entries need to be changed."
                    : $"{changesCount} SerializeReference asm change(s) across {scanResult.Count} asset file(s).";
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
                var result = AsmdefRenameService.ApplySerializeReferenceYamlAssemblyChanges(scanResult);
                statusText = $"Applied {result.WrittenYamlReplacementsCount} SerializeReference asm replacement(s) " +
                             $"in {result.WrittenYamlFilesCount} asset file(s).";
                scanResult = null;
                selectedAction = 0;
            }
            catch (Exception exception)
            {
                statusText = exception.Message;
                Log.Error(exception);
            }
        }

        private static int CountChanges(IReadOnlyList<AsmdefRenameFileChange> changes)
        {
            if (changes == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var change in changes)
            {
                count += change.ReplacementsCount;
            }

            return count;
        }
    }
}
