using UnityEditor;

namespace SerializeReferenceDropdownBridge
{
    internal static class AsmdefRenameMenu
    {
        [MenuItem("Assets/Serialize Reference Dropdown/Rename Assembly Definition", true)]
        private static bool ValidateRenameSelectedAsmdef()
        {
            return AsmdefRenameService.IsAsmdefAssetPath(AsmdefRenameService.GetSelectedAsmdefAssetPath());
        }

        [MenuItem("Assets/Serialize Reference Dropdown/Rename Assembly Definition")]
        private static void RenameSelectedAsmdef()
        {
            AsmdefRenameWindow.Open(new AsmdefRenameRequest
            {
                asmdefPath = AsmdefRenameService.GetSelectedAsmdefAssetPath()
            });
        }
    }
}
