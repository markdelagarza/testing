private static string GetSampleValue(ITypeSymbol type)
{
    if (type is INamedTypeSymbol named &&
        named.OriginalDefinition.SpecialType ==
            SpecialType.System_Nullable_T)
    {
        return GetSampleValue(named.TypeArguments[0]);
    }

    if (type.TypeKind == TypeKind.Enum)
        return $"default({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";

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

        _ => type.ToDisplayString() switch
        {
            "System.Guid" => "global::System.Guid.Empty",
            "System.DateTime" =>
                "new global::System.DateTime(2020, 1, 1)",
            "System.DateTimeOffset" =>
                "new global::System.DateTimeOffset(2020, 1, 1, 0, 0, 0, global::System.TimeSpan.Zero)",
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
