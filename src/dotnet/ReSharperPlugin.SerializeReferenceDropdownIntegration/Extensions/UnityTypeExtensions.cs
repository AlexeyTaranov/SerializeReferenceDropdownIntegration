using JetBrains.ReSharper.Psi.CSharp.Tree;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Data;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Extensions;

public static class UnityTypeExtensions
{
    public static UnityTypeData ExtractUnityTypeFromClassDeclaration(this IClassDeclaration classDeclaration)
    {
        var fullTypeName = classDeclaration?.CLRName;

        var className = fullTypeName;
        var typeNamespace = string.Empty;

        var lastDot = fullTypeName?.LastIndexOf('.');
        if (lastDot > 0)
        {
            typeNamespace = fullTypeName.Substring(0, lastDot.Value);
            className = fullTypeName.Substring(lastDot.Value + 1);
        }

        var psi = classDeclaration.GetPsiModule();

        return new UnityTypeData()
        {
            ClassName = className,
            Namespace = typeNamespace,
            AssemblyName = psi.ContainingProjectModule.Name,
        };
    }
}