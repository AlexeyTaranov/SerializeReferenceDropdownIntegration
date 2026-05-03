using System;

namespace SerializeReferenceDropdownBridge.AsmdefRename
{
    [Serializable]
    internal sealed class AsmdefRenameRequest
    {
        public string asmdefPath;
        public string oldName;
        public string newName;

    }
}
