using VTech.AsyncRefactoring.Base.CodeGraph;

namespace VTech.AsyncRefactoring.Base.Rules;
internal class GetAwaiterGetResultRule : RuleBase
{
    public GetAwaiterGetResultRule(SymbolInfoStorage symbolInfoStorage)
        : base(nameof(GetAwaiterGetResultRule), symbolInfoStorage)
    {
    }

    public override (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes)
    {
        ExpressionSyntax centralNode = nodes.Last() as ExpressionSyntax;
        centralNode = centralNode.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(centralNode);
        SyntaxNode newBlock = awaitExpression.WithTrailingTrivia(nodes[0].GetTrailingTrivia()).WithLeadingTrivia(nodes[0].GetLeadingTrivia());

        return (nodes[0], newBlock);
    }

    // Return a list of expressions that should be checked for this rule. 
    // This rule is to find AnyFunc().GetAwaiter().GetResult() where AnyFunc() returns a Task.
    protected override Func<SemanticModel, SyntaxNode, bool>[] GetExpressionCheckers()
    {
        Func<SemanticModel, SyntaxNode, bool>[] result =
        [
            (_, node) => node is InvocationExpressionSyntax,
            (_, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "GetResult",
            (_, node) => node is InvocationExpressionSyntax,
            (_, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "GetAwaiter",
            IsNodeOfTaskType
        ];

        return result;
    }
}
