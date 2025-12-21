namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

public readonly record struct UnityTypeData(string ClassName, string Namespace, string AssemblyName)
{
    public string BuildSerializeReferenceTypeString() =>
        $"class: {ClassName}, ns: {Namespace}, asm: {AssemblyName}";

    public string GetFullTypeName() => string.IsNullOrEmpty(Namespace) ? ClassName : $"{Namespace}.{ClassName}";
}