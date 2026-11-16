
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EfQueryAnalyzer.Generation;

public sealed class QueryProjectionRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitInvocationExpression(
        InvocationExpressionSyntax node)
    {
        // Only rewrite projections inside Select().
        if (node.Expression is not MemberAccessExpressionSyntax member ||
            member.Name.Identifier.ValueText != "Select")
        {
            return base.VisitInvocationExpression(node);
        }

        if (node.ArgumentList.Arguments.Count != 1)
            return base.VisitInvocationExpression(node);

        var argument = node.ArgumentList.Arguments[0];

        if (argument.Expression is not LambdaExpressionSyntax lambda)
            return base.VisitInvocationExpression(node);

        if (lambda.Body is not ObjectCreationExpressionSyntax creation)
            return base.VisitInvocationExpression(node);

        if (creation.Initializer is null ||
            creation.Initializer.Kind() != SyntaxKind.ObjectInitializerExpression ||
            creation.ArgumentList?.Arguments.Count > 0)
        {
            return base.VisitInvocationExpression(node);
        }

        // Only support simple property assignments.
        var assignments = creation.Initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .ToArray();

        if (assignments.Length != creation.Initializer.Expressions.Count ||
            assignments.Any(a =>
                !a.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                a.Left is not IdentifierNameSyntax))
        {
            return base.VisitInvocationExpression(node);
        }

        var members = assignments.Select(assignment =>
            SyntaxFactory.AnonymousObjectMemberDeclarator(
                SyntaxFactory.NameEquals(
                    SyntaxFactory.IdentifierName(
                        ((IdentifierNameSyntax)assignment.Left).Identifier)),
                assignment.Right));

        var anonymousCreation =
            SyntaxFactory.AnonymousObjectCreationExpression(
                SyntaxFactory.SeparatedList(members))
            .WithTriviaFrom(creation);

        var rewrittenLambda = lambda.ReplaceNode(
            creation,
            anonymousCreation);

        var rewrittenInvocation = node.ReplaceNode(
            lambda,
            rewrittenLambda);

        return base.VisitInvocationExpression(rewrittenInvocation);
    }

    public static ExpressionSyntax Rewrite(ExpressionSyntax expression)
    {
        return (ExpressionSyntax)new QueryProjectionRewriter()
            .Visit(expression)!;
    }
}
