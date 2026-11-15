
using Microsoft.CodeAnalysis;

namespace EfQueryAnalyzer.Generation;

public static class QueryParameterGenerator
{
    public static string? Generate(
        ITypeSymbol type,
        string parameterName)
    {
        if (!IsSupportedType(type))
            return null;

        var typeName = type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        return $"{typeName} {parameterName} = default;";
    }

    private static bool IsSupportedType(ITypeSymbol type)
    {
        // Nullable<T>
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return IsSupportedType(named.TypeArguments[0]);
        }

        // Enums
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Primitive types
        if (type.SpecialType is
            SpecialType.System_String or
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal or
            SpecialType.System_Char)
        {
            return true;
        }

        // Common .NET value types
        return type.ToDisplayString() is
            "System.Guid" or
            "System.DateTime" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan";
    }
}
