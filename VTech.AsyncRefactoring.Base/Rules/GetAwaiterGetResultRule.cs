using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.Rules;
internal class GetAwaiterGetResultRule : RuleBase
{
    public GetAwaiterGetResultRule()
        : base(nameof(GetAwaiterGetResultRule))
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
    protected override Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] GetExpressionCheckers()
    {
        Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] result =
        [
            (parent, methodSymbol, node) => node is InvocationExpressionSyntax,
            (parent, methodSymbol, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "GetResult",
            (parent, methodSymbol, node) => node is InvocationExpressionSyntax,
            (parent, methodSymbol, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "GetAwaiter",
            (parent, methodSymbol, node) => IsNodeOfTaskType(parent, methodSymbol, node)
        ];

        return result;
    }
}
