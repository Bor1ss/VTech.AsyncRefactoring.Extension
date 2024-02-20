namespace VTech.AsyncRefactoring.Base.Utils;
internal static class SymbolExtensions
{
    public static string GetFullyQualifiedName(this IMethodSymbol symbol)
    {
        return symbol.ContainingType is not null
            ? $"{symbol.ContainingType.Name}.{symbol.Name}"
            : symbol.Name;
    }

    public static bool IsTaskType(this TypeInfo typeInfo)
    {
        var typeInfoType = typeInfo.Type;
        return IsTaskType(typeInfoType);
    }

    public static bool IsFuncReturnedTaskType(this TypeInfo typeInfo)
    {
        var typeInfoType = typeInfo.Type;
        return IsFuncReturnedTaskType(typeInfoType);
    }

    public static bool IsTaskType(this ITypeSymbol symbol)
    {
        string @namespace = symbol?.ContainingNamespace?.ToString();
        string type = symbol?.Name;
        return (type == "Task" || type.StartsWith("Task<")) && @namespace == "System.Threading.Tasks";
    }

    public static bool IsFuncReturnedTaskType(this ITypeSymbol symbol)
    {
        bool isFunc = string.Equals(symbol?.ContainingNamespace?.ToString(), "System")
            && string.Equals(symbol?.Name, "Func");

        if(!isFunc)
        {
            return false;
        }

        INamedTypeSymbol namedTypeSymbol = symbol as INamedTypeSymbol;

        return namedTypeSymbol.TypeArguments.Last().IsTaskType();
    }
}
