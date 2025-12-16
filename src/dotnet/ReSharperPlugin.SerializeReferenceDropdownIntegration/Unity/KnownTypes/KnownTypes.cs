using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.KnownTypes;

public class KnownTypes
{
    // UnityEngine.Serialization
    public static readonly IClrTypeName movedFromAttribute =
        new ClrTypeName("UnityEngine.Scripting.APIUpdating.MovedFromAttribute");
}