using UnityEditor;
using UnityEngine;

namespace SerializeReferenceDropdownBridge.AsmdefRename
{
    [InitializeOnLoad]
    internal static class AsmdefRenameInspectorExtension
    {
        static AsmdefRenameInspectorExtension()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawAsmdefRenameHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawAsmdefRenameHeader;
        }

        private static void DrawAsmdefRenameHeader(UnityEditor.Editor editor)
        {
            if (editor == null || editor.target == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(editor.target);
            if (!AsmdefRenameService.IsAsmdefAssetPath(assetPath))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Serialize Reference Dropdown Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Rename asmdef and SerializeReference YAML"))
            {
                AsmdefRenameWindow.Open(new AsmdefRenameRequest
                {
                    asmdefPath = assetPath
                });
            }

            if (GUILayout.Button("Move SerializeReference YAML to this asmdef"))
            {
                AsmdefSerializeReferenceRetargetWindow.Open(assetPath);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
