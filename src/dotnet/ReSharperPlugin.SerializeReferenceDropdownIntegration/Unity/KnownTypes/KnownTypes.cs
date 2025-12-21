using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.KnownTypes;

public static class KnownTypes
{
    public static readonly IClrTypeName movedFromAttribute =
        new ClrTypeName("UnityEngine.Scripting.APIUpdating.MovedFromAttribute");
}