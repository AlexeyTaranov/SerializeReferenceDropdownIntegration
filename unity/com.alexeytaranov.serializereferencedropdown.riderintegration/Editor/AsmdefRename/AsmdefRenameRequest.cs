using System;

namespace SerializeReferenceDropdownBridge
{
    [Serializable]
    internal sealed class AsmdefRenameRequest
    {
        public string asmdefPath;
        public string oldName;
        public string newName;

        public static AsmdefRenameRequest FromPayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new AsmdefRenameRequest();
            }

            if (payload.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                return UnityEngine.JsonUtility.FromJson<AsmdefRenameRequest>(payload);
            }

            return new AsmdefRenameRequest
            {
                asmdefPath = payload
            };
        }
    }
}
