using System;

namespace SerializeReferenceDropdownBridge
{
    [Serializable]
    internal sealed class SrdBridgeResponse
    {
        public int version;
        public string status;
        public string message;

        public static SrdBridgeResponse Create(string status, string message)
        {
            return new SrdBridgeResponse
            {
                version = 1,
                status = status,
                message = message
            };
        }
    }
}
