
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

        // Identify external expressions.
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

        // Preserve method names in calls such as:
        // numbers.Contains(o.Item)
        //
        // We want to replace "numbers", not "numbers.Contains".
        if (expression is MemberAccessExpressionSyntax methodAccess &&
            methodAccess.Parent is InvocationExpressionSyntax invocation &&
            invocation.Expression == methodAccess)
        {
            return false;
        }

        // Handle constants:
        // Truck
        // Shop.Truck
        // Constants.Shop.Truck
        var expressionSymbol =
            model.GetSymbolInfo(expression).Symbol;

        if (expressionSymbol is IFieldSymbol { IsConst: true })
            return true;

        // Walk to the root of the property chain.
        // Handle null-forgiving operators and parentheses.
        ExpressionSyntax root = expression;

        while (true)
        {
            switch (root)
            {
                case MemberAccessExpressionSyntax member:
                    root = member.Expression;
                    break;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(
                        SyntaxKind.SuppressNullableWarningExpression):
                    root = postfix.Operand;
                    break;

                case ParenthesizedExpressionSyntax parenthesized:
                    root = parenthesized.Expression;
                    break;

                default:
                    goto RootFound;
            }
        }

    RootFound:

        // Handle expressions such as this._service.Id.
        if (root is ThisExpressionSyntax)
        {
            var type = model.GetTypeInfo(expression).Type;

            return type is not null &&
                QueryParameterGenerator.Generate(
                    type,
                    "parameter") is not null;
        }

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
