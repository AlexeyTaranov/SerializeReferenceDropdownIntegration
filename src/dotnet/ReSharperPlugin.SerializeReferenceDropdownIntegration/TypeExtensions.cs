namespace ReSharperPlugin.SerializeReferenceDropdownIntegration;

public static class TypeExtensions
{
    public static string MakeType(string typeName, string asmName)
    {
        return $"{typeName},{asmName}".Replace(" ", "");
    }
}