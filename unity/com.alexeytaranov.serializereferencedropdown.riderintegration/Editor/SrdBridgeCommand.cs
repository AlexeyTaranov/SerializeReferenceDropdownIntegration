using System;

namespace SerializeReferenceDropdownBridge.Editor
{
    [Serializable]
    internal sealed class SrdBridgeCommand
    {
        public int version;
        public string command;
        public string payload;
    }
}
