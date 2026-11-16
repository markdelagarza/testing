// Handle arrays.
if (type is IArrayTypeSymbol array &&
    IsSupportedType(array.ElementType))
{
    var elementType = array.ElementType.ToDisplayString(
        SymbolDisplayFormat.FullyQualifiedFormat);

    return $"{elementType}[] {parameterName} = [];";
}

// Handle common generic collections.
if (type is INamedTypeSymbol named &&
    named.IsGenericType &&
    named.TypeArguments.Length == 1 &&
    IsSupportedType(named.TypeArguments[0]))
{
    var collectionType = named.OriginalDefinition
        .ToDisplayString();

    if (collectionType is
        "System.Collections.Generic.List<T>" or
        "System.Collections.Generic.IEnumerable<T>" or
        "System.Collections.Generic.ICollection<T>" or
        "System.Collections.Generic.IReadOnlyCollection<T>" or
        "System.Collections.Generic.IReadOnlyList<T>" or
        "System.Collections.Generic.IList<T>")
    {
        var typeName = type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);

        return $"{typeName} {parameterName} = [];";
    }
}
