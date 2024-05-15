using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.Rules;
internal class ResultRule : RuleBase
{
    public ResultRule(SymbolInfoStorage symbolInfoStorage)
        : base(nameof(ResultRule), symbolInfoStorage)
    {
    }

    public override (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes)
    {
        ExpressionSyntax centralNode = nodes.Last() as ExpressionSyntax;
        centralNode = centralNode.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
        AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(centralNode);
        SyntaxNode newBlock = awaitExpression
            .WithTrailingTrivia(nodes[0].GetTrailingTrivia())
            .WithLeadingTrivia(nodes[0].GetLeadingTrivia());

        return (nodes[0], newBlock);
    }

    protected override Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] GetExpressionCheckers()
    {
        Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] result =
        [
            (parent, methodSymbol, node) => node is MemberAccessExpressionSyntax maes && maes.Name.ToString() == "Result",
            (parent, methodSymbol, node) => IsNodeOfTaskType(parent, node)
        ];

        return result;
    }
}
