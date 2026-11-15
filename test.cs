
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EfQueryAnalyzer.Generation;

public static class QueryParameterRewriter
{
    public static QueryParameterGenerationResult Rewrite(
        InvocationExpressionSyntax query,
        SemanticModel model)
    {
        var replacements =
            new Dictionary<ExpressionSyntax, ExpressionSyntax>();

        var declarations = new List<string>();
        var parameterIndex = 0;

        // Identify parameters declared by lambdas within
        // the query expression.
        var lambdaParameters = query
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .SelectMany(lambda => lambda switch
            {
                SimpleLambdaExpressionSyntax simple =>
                    [simple.Parameter],

                ParenthesizedLambdaExpressionSyntax parenthesized =>
                    parenthesized.ParameterList.Parameters,

                _ => Enumerable.Empty<ParameterSyntax>()
            })
            .Select(model.GetDeclaredSymbol)
            .Where(symbol => symbol is not null)
            .ToHashSet(SymbolEqualityComparer.Default);

        // Identify the outermost external variable or
        // property expressions.
        var candidates = query
            .DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(expression =>
                expression is IdentifierNameSyntax or
                    MemberAccessExpressionSyntax)
            .Where(expression =>
                IsExternalExpression(
                    expression,
                    model,
                    lambdaParameters))
            .Where(expression =>
                !expression.Ancestors()
                    .OfType<ExpressionSyntax>()
                    .Any(parent =>
                        IsExternalExpression(
                            parent,
                            model,
                            lambdaParameters)))
            .ToList();

        foreach (var expression in candidates)
        {
            var type = model.GetTypeInfo(expression).Type;

            if (type is null || !IsSupportedType(type))
                continue;

            parameterIndex++;

            var parameterName =
                $"parameter_{parameterIndex}";

            var typeName = type.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat);

            declarations.Add(
                $"{typeName} {parameterName} = default;");

            replacements[expression] =
                SyntaxFactory.IdentifierName(parameterName)
                    .WithTriviaFrom(expression);
        }

        var rewritten = query.ReplaceNodes(
            replacements.Keys,
            (original, _) => replacements[original]);

        return new QueryParameterGenerationResult(
            rewritten,
            declarations,
            true,
            null);
    }

    private static bool IsExternalExpression(
        ExpressionSyntax expression,
        SemanticModel model,
        HashSet<ISymbol> lambdaParameters)
    {
        if (expression is not IdentifierNameSyntax &&
            expression is not MemberAccessExpressionSyntax)
        {
            return false;
        }

        // Walk back to the root of the expression.
        var root = expression;

        while (root is MemberAccessExpressionSyntax member)
            root = member.Expression;

        if (root is not IdentifierNameSyntax identifier)
            return false;

        var symbol = model.GetSymbolInfo(identifier).Symbol;

        if (symbol is not ILocalSymbol &&
            symbol is not IParameterSymbol)
        {
            return false;
        }

        // Preserve lambda entity parameters.
        if (lambdaParameters.Contains(symbol))
            return false;

        // Preserve variables declared inside the query.
        if (symbol.Locations.Any(location =>
            location.IsInSource &&
            location.SourceTree == expression.SyntaxTree &&
            query.Span.Contains(location.SourceSpan)))
        {
            return false;
        }

        return true;
    }

    private static bool IsSupportedType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return IsSupportedType(named.TypeArguments[0]);
        }

        if (type.TypeKind == TypeKind.Enum)
            return true;

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

        return type.ToDisplayString() is
            "System.Guid" or
            "System.DateTime" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan";
    }
}
