using System;

namespace SerializeReferenceDropdownBridge
{
    [Serializable]
    internal sealed class AsmdefRenameRequest
    {
        public string asmdefPath;
        public string oldName;
        public string newName;

    }
}
