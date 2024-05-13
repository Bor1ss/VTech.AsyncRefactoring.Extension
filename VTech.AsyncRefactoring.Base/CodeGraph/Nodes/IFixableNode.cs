using VTech.AsyncRefactoring.Base.Rules;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
public interface IFixableNode
{
    int Depth { get; }
    Location Location { get; }
    void DetectIssues(IReadOnlyList<IRule> rules);
    void PrepareFixes(SymbolInfoStorage symbolInfoStorage);
}

public abstract class FixableNodeBase : IFixableNode
{
    private readonly static Type[] _typesToSkipWhileErrorDetection = [
        typeof(LocalFunctionStatementSyntax),
        typeof(SimpleLambdaExpressionSyntax),
        typeof(ParenthesizedLambdaExpressionSyntax),
        typeof(VariableDeclarationSyntax),
        typeof(VariableDeclaratorSyntax),
    ];

    protected readonly List<DetectedIssue> _detectedIssues = [];
    private readonly SemanticModel _semanticModel;
    protected bool _isIssueDetectionStarted = false;

    protected FixableNodeBase(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    protected abstract bool ShouldSkipFixing { get; }
    protected abstract SyntaxNode Body { get; }
    public abstract int Depth { get; }
    public abstract Location Location { get; }
    public void DetectIssues(IReadOnlyList<IRule> rules)
    {
        if(ShouldSkipFixing)
        {
            return;
        }

        _isIssueDetectionStarted = true;

        foreach (var rule in rules)
        {
            foreach (var expr in Body.DescendantNodes((s) => !_typesToSkipWhileErrorDetection.Contains(s.GetType())))
            {
                ExpressionProcessingResult result = rule.Process(_semanticModel, expr);

                if (result == ExpressionProcessingResult.LastAdded)
                {
                    _detectedIssues.Add(new DetectedIssue(rule, rule.GetApplicableNodes()));

                    rule.Flush();
                    continue;
                }

                if (result == ExpressionProcessingResult.Added || result == ExpressionProcessingResult.Skipped)
                {
                    continue;
                }

                rule.Flush();
            }

            rule.Flush();
        }

        if (_detectedIssues.Count == 0)
        {
            return;
        }

        SetAsyncIsNeeded();
    }

    public abstract void SetAsyncIsNeeded(MethodNode method = null);

    public abstract void PrepareFixes(SymbolInfoStorage symbolInfoStorage);

    protected class DetectedIssue
    {
        public DetectedIssue(IRule Rule, List<SyntaxNode> Nodes)
        {
            this.Rule = Rule;
            this.Nodes = Nodes;
        }

        public IRule Rule { get; }
        public List<SyntaxNode> Nodes { get; }
    }
}