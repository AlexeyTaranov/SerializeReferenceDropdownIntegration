using System;

namespace SerializeReferenceDropdownBridge
{
    [Serializable]
    internal sealed class SrdBridgeCommand
    {
        public int version;
        public string command;
        public string payload;
        public string replyPipe;
    }
}
