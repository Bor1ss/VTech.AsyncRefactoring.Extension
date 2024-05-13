using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Base.Rules;

public enum ExpressionProcessingResult
{
    Undefined = 0,
    NotApplicable = 1,
    Skipped = 2,
    Added = 3,
    LastAdded = 4
}

public interface IRule
{
    string Name { get; }
    void Flush();
    ExpressionProcessingResult Process(SemanticModel semanticModel, SyntaxNode node);
    List<SyntaxNode> GetApplicableNodes();
    (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes);
}

internal abstract class RuleBase : IRule
{
    private readonly List<SyntaxNode> _expressions = [];
    private readonly Func<SemanticModel, SyntaxNode, bool>[] _expressionCheckers;
    private readonly SymbolInfoStorage _symbolInfoStorage;
    private ExpressionProcessingResult _lastActionResult = ExpressionProcessingResult.Undefined;
    private int _processingIndex = 0;

    protected RuleBase(string name, SymbolInfoStorage symbolInfoStorage)
    {
        Name = name;
        _symbolInfoStorage = symbolInfoStorage;
        _expressionCheckers = GetExpressionCheckers();
    }

    public string Name { get; private set; }

    protected abstract Func<SemanticModel, SyntaxNode, bool>[] GetExpressionCheckers();

    public void Flush()
    {
        _expressions.Clear();
        _processingIndex = 0;
        _lastActionResult = ExpressionProcessingResult.Undefined;
    }

    public ExpressionProcessingResult Process(SemanticModel semanticModel, SyntaxNode node)
    {
        if (_lastActionResult == ExpressionProcessingResult.LastAdded)
        {
            throw new InvalidOperationException();
        }

        if (IsSkippable(node))
        {
            if (_expressions.Count > 0)
            {
                _expressions.Add(node);

                _lastActionResult = ExpressionProcessingResult.Skipped;
                return _lastActionResult;
            }

            _lastActionResult = ExpressionProcessingResult.NotApplicable;
            return _lastActionResult;
        }

        bool isFound = _expressionCheckers[_processingIndex](semanticModel, node);

        if (!isFound)
        {
            if (_processingIndex == 0)
            {
                _lastActionResult = ExpressionProcessingResult.NotApplicable;
                return _lastActionResult;
            }

            Flush();

            return Process(semanticModel, node);
        }

        _processingIndex++;

        _expressions.Add(node);

        if (_processingIndex == _expressionCheckers.Length)
        {
            _lastActionResult = ExpressionProcessingResult.LastAdded;
            return _lastActionResult;
        }

        _lastActionResult = ExpressionProcessingResult.Added;
        return _lastActionResult;
    }

    public List<SyntaxNode> GetApplicableNodes()
    {
        if (_lastActionResult != ExpressionProcessingResult.LastAdded)
        {
            throw new InvalidOperationException();
        }

        return _expressions.ToList();
    }

    private bool IsSkippable(SyntaxNode node)
    {
        return false;
    }

    public abstract (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes);

    protected bool IsNodeOfTaskType(SemanticModel semanticModel, SyntaxNode node)
    {
        TypeInfo typeInfo = semanticModel.GetTypeInfo(node);
        return typeInfo.IsTaskType() || typeInfo.IsFuncReturnedTaskType();
    }
}
