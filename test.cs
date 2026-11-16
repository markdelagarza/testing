
using Microsoft.CodeAnalysis;

namespace EfQueryAnalyzer.Generation;

public static class QueryParameterGenerator
{
    public static string? Generate(
        ITypeSymbol type,
        string parameterName)
    {
        // Arrays
        if (type is IArrayTypeSymbol array &&
            array.Rank == 1 &&
            IsSupportedType(array.ElementType))
        {
            var elementType = GetTypeName(array.ElementType);
            var sampleValue = GetSampleValue(array.ElementType);

            return $"{elementType}[] {parameterName} = [{sampleValue}];";
        }

        // Generic collections
        if (type is INamedTypeSymbol named &&
            named.IsGenericType &&
            named.TypeArguments.Length == 1 &&
            IsSupportedType(named.TypeArguments[0]) &&
            IsSupportedCollection(named))
        {
            var typeName = GetTypeName(type);
            var sampleValue = GetSampleValue(named.TypeArguments[0]);

            return $"{typeName} {parameterName} = [{sampleValue}];";
        }

        // Scalar values
        if (!IsSupportedType(type))
            return null;

        var scalarType = GetTypeName(type);

        return $"{scalarType} {parameterName} = default;";
    }

    private static bool IsSupportedCollection(
        INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return definition is
            "global::System.Collections.Generic.List<T>" or
            "global::System.Collections.Generic.IEnumerable<T>" or
            "global::System.Collections.Generic.ICollection<T>" or
            "global::System.Collections.Generic.IReadOnlyCollection<T>" or
            "global::System.Collections.Generic.IReadOnlyList<T>" or
            "global::System.Collections.Generic.IList<T>" or
            "global::System.Collections.Generic.ISet<T>" or
            "global::System.Collections.Generic.HashSet<T>";
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

    private static string GetTypeName(ITypeSymbol type)
    {
        return type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static string GetSampleValue(ITypeSymbol type)
    {
        // Nullable<T>
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return GetSampleValue(named.TypeArguments[0]);
        }

        // Enums
        if (type.TypeKind == TypeKind.Enum)
        {
            return $"default({GetTypeName(type)})";
        }

        return type.SpecialType switch
        {
            SpecialType.System_String => "\"sample\"",
            SpecialType.System_Boolean => "true",
            SpecialType.System_Char => "'A'",

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
            SpecialType.System_Decimal => "1",

            SpecialType.System_Single => "1f",
            SpecialType.System_Double => "1d",

            _ => type.ToDisplayString() switch
            {
                "System.Guid" =>
                    "global::System.Guid.Empty",

                "System.DateTime" =>
                    "new global::System.DateTime(2020, 1, 1)",

                "System.DateTimeOffset" =>
                    "new global::System.DateTimeOffset(" +
                    "2020, 1, 1, 0, 0, 0, " +
                    "global::System.TimeSpan.Zero)",

                "System.DateOnly" =>
                    "new global::System.DateOnly(2020, 1, 1)",

                "System.TimeOnly" =>
                    "new global::System.TimeOnly(12, 0)",

                "System.TimeSpan" =>
                    "global::System.TimeSpan.FromMinutes(1)",

                _ => "default"
            }
        };
    }
}
