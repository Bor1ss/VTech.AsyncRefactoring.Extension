using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.Rules;

internal class WaitRule : RuleBase
{
    public WaitRule()
        : base(nameof(WaitRule))
    {
    }

    public override (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes)
    {
        ExpressionSyntax centralNode = nodes.Last() as ExpressionSyntax;
        centralNode = centralNode.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(centralNode);
        SyntaxNode expressionStatement = SyntaxFactory.ExpressionStatement(awaitExpression);
        SyntaxNode newBlock = expressionStatement.WithTrailingTrivia(nodes[0].GetTrailingTrivia()).WithLeadingTrivia(nodes[0].GetLeadingTrivia());

        return (nodes[0], newBlock);
    }

    // Return a list of expressions that should be checked for this rule. 
    // This rule is to find AnyFunc().Wait() where AnyFunc() returns a Task.
    protected override Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] GetExpressionCheckers()
    {
        Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] result =
        [
            (parent, methodSymbol, node) => node is ExpressionStatementSyntax,
            (parent, methodSymbol, node) => node is InvocationExpressionSyntax,
            (parent, methodSymbol, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "Wait",
            (parent, methodSymbol, node) => IsNodeOfTaskType(parent, methodSymbol, node)
        ];

        return result;
    }
}
