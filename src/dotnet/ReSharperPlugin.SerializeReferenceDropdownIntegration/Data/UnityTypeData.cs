namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

public struct UnityTypeData
{
    public string AssemblyName;
    public string Namespace;
    public string ClassName;

    public readonly string BuildSerializeReferenceTypeString() =>
        $"class: {ClassName}, ns: {Namespace}, asm: {AssemblyName}";
}