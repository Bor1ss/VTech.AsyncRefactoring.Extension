using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Base.Rules;

internal enum ExpressionProcessingResult
{
    Undefined = 0,
    NotApplicable = 1,
    Skipped = 2,
    Added = 3,
    LastAdded = 4
}

internal interface IRule
{
    string Name { get; }
    void Flush();
    ExpressionProcessingResult Process(BaseTypeDeclarationNode parent, IMethodSymbol methodSymbol, SyntaxNode node);
    List<SyntaxNode> GetApplicableNodes();
    (SyntaxNode oldNode, SyntaxNode newNode) Fix(List<SyntaxNode> nodes);
}

internal abstract class RuleBase : IRule
{
    private readonly List<SyntaxNode> _expressions = [];
    private readonly Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] _expressionCheckers;
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

    protected abstract Func<BaseTypeDeclarationNode, IMethodSymbol, SyntaxNode, bool>[] GetExpressionCheckers();

    public void Flush()
    {
        _expressions.Clear();
        _processingIndex = 0;
        _lastActionResult = ExpressionProcessingResult.Undefined;
    }

    public ExpressionProcessingResult Process(BaseTypeDeclarationNode parent, IMethodSymbol methodSymbol, SyntaxNode node)
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

        bool isFound = _expressionCheckers[_processingIndex](parent, methodSymbol, node);

        if (!isFound)
        {
            if (_processingIndex == 0)
            {
                _lastActionResult = ExpressionProcessingResult.NotApplicable;
                return _lastActionResult;
            }

            Flush();

            return Process(parent, methodSymbol, node);
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

    protected bool IsNodeOfTaskType(BaseTypeDeclarationNode parent, IMethodSymbol methodSymbol, SyntaxNode node)
    {
        TypeInfo typeInfo = parent.Parent.SemanticModel.GetTypeInfo(node);
        return typeInfo.IsTaskType() || typeInfo.IsFuncReturnedTaskType();
        if (typeInfo.IsTaskType() || typeInfo.IsFuncReturnedTaskType())
        {
            return true;
        }

        if (node is InvocationExpressionSyntax ies)
        {
            MethodNode method = _symbolInfoStorage[ies];

            if (method is not null)
            {
                return method.IsTaskReturned;
            }

            IdentifierNameSyntax ident = ies.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();
            if(ident is not null)
            {
                var typeSymbol = GetTypeSymbol(parent, ident);

                _ = typeSymbol;

                return typeSymbol?.IsTaskType() == true || typeSymbol?.IsFuncReturnedTaskType() == true;
            }

            return false;
        };

        if (node is IdentifierNameSyntax identifierName)
        {
            return GetTypeSymbol(parent, identifierName)?.IsTaskType() == true;
        }

        return false;
    }

    private ITypeSymbol GetTypeSymbol(BaseTypeDeclarationNode parent, IdentifierNameSyntax identifierName)
    {
        foreach (var ancestor in identifierName.Ancestors())
        {
            IEnumerable<VariableDeclarationSyntax> variableDeclarationSyntaxes = ancestor.DescendantNodes()
                .Where(x => x.SpanStart < identifierName.SpanStart)
                .OfType<VariableDeclarationSyntax>()
                .Reverse();

            foreach (var variableDeclaration in variableDeclarationSyntaxes)
            {
                foreach (var variableDeclarator in variableDeclaration.Variables)
                {
                    var variableName = variableDeclarator.Identifier.ToString();

                    if (!string.Equals(variableName, identifierName.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return parent.Parent.SemanticModel.GetTypeInfo(variableDeclarator.Initializer.Value).Type;
                }
            }

            if (ancestor is MethodDeclarationSyntax methodDeclaration)
            {
                ParameterSyntax parameter = methodDeclaration.ParameterList.Parameters.FirstOrDefault(x => x.Identifier.ToString() == identifierName.ToString());
                if (parameter is not null)
                {
                    return parent.Parent.SemanticModel.GetTypeInfo(parameter.Type).Type;
                }
            }

            if(ancestor is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
            {
                var semanticModel = parent.Parent.SemanticModel;
                var typeInfo = semanticModel.GetTypeInfo(ancestor);
                var symb = semanticModel.GetSymbolInfo(ancestor);

                if (symb.Symbol is IMethodSymbol methodSymbol)
                {
                    return methodSymbol.ReturnType;
                }

            }
        }

        return default;
    }
}
