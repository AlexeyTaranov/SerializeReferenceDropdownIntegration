using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity;

public class KnownTypes
{
    // UnityEngine.Serialization
    public static readonly IClrTypeName MovedFromAttribute =
        new ClrTypeName("UnityEngine.Scripting.APIUpdating.MovedFromAttribute");
}