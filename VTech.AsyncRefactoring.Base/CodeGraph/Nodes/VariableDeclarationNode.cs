
namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
public class VariableDeclarationNode : FixableNodeBase, IDeclarationParent
{
    private readonly BaseTypeDeclarationNode _baseTypeDeclarationNode;
    private readonly MethodNode _baseMethod;
    private readonly VariableDeclaratorSyntax _variableDeclaratorSyntax;
    private readonly ISymbol _declarationSymbol;
    private readonly TypeInfo _returnType;
    private readonly List<object> _usages = [];
    private readonly List<object> _innerSymbols = [];

    private readonly List<MethodNode> _internalMethods = [];

    private readonly List<VariableDeclarationNode> _variables = [];

    private readonly List<(IDeclarationParent, CSharpSyntaxNode)> _variableInvoications = [];

    public VariableDeclarationNode(VariableDeclaratorSyntax node, ISymbol declarationSymbol, TypeInfo returnType, BaseTypeDeclarationNode baseTypeDeclarationNode, MethodNode method)
        : base(baseTypeDeclarationNode.Parent.SemanticModel)
    {
        _baseTypeDeclarationNode = baseTypeDeclarationNode;
        _baseMethod = method;
        _variableDeclaratorSyntax = node;
        _declarationSymbol = declarationSymbol;
        _returnType = returnType;
    }

    protected override bool ShouldSkipFixing { get; }
    protected override SyntaxNode Body => _variableDeclaratorSyntax;
    public IReadOnlyList<MethodNode> Methods => _internalMethods;
    public override Location Location => Body.GetLocation();
    public override int Depth { get; } = 0;

    public void AddMethod(MethodNode method)
    {
        _internalMethods.Add(method);
    }

    public void AddVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        _variables.Add(variableDeclaration);
    }

    public void GetAllProcessableNodes(HashSet<IFixableNode> result)
    {
        foreach (MethodNode method in _internalMethods)
        {
            if (!result.Add(method))
            {
                continue;
            }

            method.GetAllProcessableNodes(result);
        }

        foreach (VariableDeclarationNode variable in _variables)
        {
            if (!result.Add(variable))
            {
                continue;
            }

            variable.GetAllProcessableNodes(result);
        }
    }

    public bool IsAsyncNeeded { get; private set; }
    public bool IsAsynchronized { get; private set; }
    public bool NeedsAsynchronization => IsAsyncNeeded || _detectedIssues.Count > 0;//|| _methodInvocationAsynchronizationNeeded.Count > 0;
    private readonly Dictionary<SyntaxNode, SyntaxNode> _nodeReplacements = [];
    public override void PrepareFixes(SymbolInfoStorage symbolInfoStorage)
    {
        if (!NeedsAsynchronization || IsAsynchronized)
        {
            return;
        }

        Dictionary<SyntaxNode, SyntaxNode> nodeReplacements = [];

        foreach (var key in _nodeReplacements.Keys)
        {
            nodeReplacements.Add(key, _nodeReplacements[key]);
        }

        //var newMethodDeclaration = _methodDeclaration.ReplaceSyntax(nodeReplacements, tokenReplacements, null);

        //if (_methodDeclaration.Body is not null && !_variableDeclaratorSyntax.Modifiers.Any(x => x.Text == "async"))
        //{
        //    newMethodDeclaration = newMethodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        //}

        //if (isChanged)
        //{
        //    if (_parentMethod is not null)
        //    {
        //        _parentMethod.Replace(_methodDeclaration.Node, newMethodDeclaration.Node);
        //    }
        //    else
        //    {
        //        _parent.Parent.Replace(_methodDeclaration.Node, newMethodDeclaration.Node);
        //    }
        //}
    }

    public override void SetAsyncIsNeeded(MethodNode method = null)
    {
        if (!NeedsAsynchronization || IsAsynchronized)
        {
            return;
        }

        foreach (var (parent, _) in _variableInvoications)
        {
            //parent.Se
        }
    }
}
