using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.Rules;

internal class WaitRule : RuleBase
{
    public WaitRule(SymbolInfoStorage symbolInfoStorage)
        : base(nameof(WaitRule), symbolInfoStorage)
    {
    }

    public override (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes)
    {
        ExpressionSyntax centralNode = nodes.Last() as ExpressionSyntax;
        centralNode = centralNode.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(centralNode);
        //SyntaxNode expressionStatement = SyntaxFactory.ExpressionStatement(awaitExpression);
        SyntaxNode newBlock = awaitExpression.WithTrailingTrivia(nodes[0].GetTrailingTrivia()).WithLeadingTrivia(nodes[0].GetLeadingTrivia());

        return (nodes[0], newBlock);
    }

    // Return a list of expressions that should be checked for this rule. 
    // This rule is to find AnyFunc().Wait() where AnyFunc() returns a Task.
    protected override Func<SemanticModel, SyntaxNode, bool>[] GetExpressionCheckers()
    {
        Func<SemanticModel, SyntaxNode, bool>[] result =
        [
            (_, node) => node is InvocationExpressionSyntax,
            (_, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "Wait",
            IsNodeOfTaskType
        ];

        return result;
    }
}
