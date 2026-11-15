
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

        // Identify parameters declared inside query lambdas.
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
            .Select(parameter => model.GetDeclaredSymbol(parameter))
            .OfType<IParameterSymbol>()
            .Cast<ISymbol>()
            .ToHashSet(SymbolEqualityComparer.Default);

        // Find the outermost external expressions.
        var candidates = query
            .DescendantNodes()
            .OfType<ExpressionSyntax>()
            .Where(expression =>
                expression is IdentifierNameSyntax or
                    MemberAccessExpressionSyntax)
            .Where(expression =>
                IsExternalExpression(
                    expression,
                    query,
                    model,
                    lambdaParameters))
            .Where(expression =>
                !expression.Ancestors()
                    .OfType<ExpressionSyntax>()
                    .Any(parent =>
                        IsExternalExpression(
                            parent,
                            query,
                            model,
                            lambdaParameters)))
            .ToList();

        foreach (var expression in candidates)
        {
            var type = model.GetTypeInfo(expression).Type;

            if (type is null)
                continue;

            var parameterName =
                $"parameter_{parameterIndex + 1}";

            var declaration = QueryParameterGenerator.Generate(
                type,
                parameterName);

            if (declaration is null)
                continue;

            parameterIndex++;

            declarations.Add(declaration);

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
        InvocationExpressionSyntax query,
        SemanticModel model,
        HashSet<ISymbol> lambdaParameters)
    {
        if (expression is not IdentifierNameSyntax &&
            expression is not MemberAccessExpressionSyntax)
        {
            return false;
        }

        // Walk to the root of the property chain.
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

        // Preserve entity lambda parameters.
        if (lambdaParameters.Contains(symbol))
            return false;

        // Preserve variables declared within the query.
        if (symbol.Locations.Any(location =>
            location.IsInSource &&
            location.SourceTree == query.SyntaxTree &&
            query.Span.Contains(location.SourceSpan)))
        {
            return false;
        }

        return true;
    }
}
