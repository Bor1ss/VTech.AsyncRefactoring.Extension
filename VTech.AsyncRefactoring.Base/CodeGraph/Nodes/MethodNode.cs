using System.Collections.Immutable;

using VTech.AsyncRefactoring.Base.Rules;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class MethodNode
{
    private readonly BaseTypeDeclarationNode _parent;
    private readonly MethodDeclarationSyntaxWrapperBase _methodDeclaration;
    private readonly IMethodSymbol _method;

    private readonly List<ISymbol> _invocations = [];

    private MethodNode _overidedMethod;
    private readonly List<MethodNode> _implementedMethods = [];

    private readonly List<MethodNode> _overridedByMethods = [];
    private readonly List<MethodNode> _implemenetedByMethods = [];

    private readonly List<MethodNode> _invokedMethods = [];
    private readonly List<MethodNode> _invokedByMethods = [];

    private readonly List<DetectedIssue> _detectedIssues = [];
    private readonly HashSet<MethodNode> _methodInvocationAsynchronizationNeeded = [];

    private readonly MethodNode _parentMethod;
    private readonly List<MethodNode> _internalMethods = [];

    private MethodNode(IMethodSymbol method, BaseTypeDeclarationNode parent)
    {
        _method = method;
        _parent = parent;

        Signature = new(method);
    }

    public MethodNode(LocalFunctionStatementSyntax node, IMethodSymbol method, BaseTypeDeclarationNode parent, MethodNode parentMethod)
        : this(method, parent)
    {
        _methodDeclaration = new LocalMethodDeclarationSyntaxWrapper(node);
        _parentMethod = parentMethod;
        Depth = _parentMethod.Depth + 1;
    }

    public MethodNode(MethodDeclarationSyntax node, IMethodSymbol method, BaseTypeDeclarationNode parent)
        : this(method, parent)
    {
        _methodDeclaration = new MethodDeclarationSyntaxWrapper(node);
    }

    public string Id => _method.Name;
    private MethodSignature Signature { get; }
    internal IMethodSymbol Symbol => _method;
    public bool IsTaskReturned => _method.ReturnType.IsTaskType();
    public bool IsAsyncNeeded { get; private set; }
    public bool IsAsynchronized { get; private set; }
    public int Depth { get; private set; } = 0;
    public IReadOnlyList<MethodNode> InternalMethods => _internalMethods;


    public List<MethodNode> GetRelatedMethods()
    {
        List<MethodNode> relatedMethods =
        [
            _overidedMethod,
            _parentMethod,
            .. _implementedMethods,
            .. _overridedByMethods,
            .. _implemenetedByMethods,
            .. _invokedMethods,
            .. _invokedByMethods,
            .. _internalMethods,
        ];

        return relatedMethods
            .Distinct()
            .Where(x => x is not null)
            .ToList();
    }

    public void AddInternalMethod(MethodNode method)
    {
        _internalMethods.Add(method);
    }
    public void AddInvocation(ISymbol invocationSymbol)
    {
        _invocations.Add(invocationSymbol);
    }
    private void AddOverrider(MethodNode method)
    {
        _overridedByMethods.Add(method);
    }
    private void AddImplementer(MethodNode method)
    {
        _implemenetedByMethods.Add(method);
    }

    internal void CompleteReferences(Dictionary<ISymbol, MethodNode> symbolMethodMap)
    {
        if (_method.OverriddenMethod is not null)
        {
            _overidedMethod = symbolMethodMap[_method.OverriddenMethod];
            _overidedMethod?.AddOverrider(this);
        }
        else
        {
            foreach (var @base in _parent.Bases)
            {
                MethodNode implementedMethod = @base.Methods.FirstOrDefault(x => x.Signature.Equals(Signature));
                if (implementedMethod is not null)
                {
                    _implementedMethods.Add(implementedMethod);
                    implementedMethod.AddImplementer(this);
                }
            }
        }

        foreach (var invocation in _invocations)
        {
            if (!symbolMethodMap.ContainsKey(invocation))
            {
                continue;
            }
            var t = symbolMethodMap[invocation];
            _invokedMethods.Add(t);
            t.AddInvoker(this);
        }
    }

    private void AddInvoker(MethodNode method)
    {
        _invokedByMethods.Add(method);
    }

    internal void DetectIssues(IReadOnlyList<IRule> rules)
    {
        if (_method is not IMethodSymbol methodSymbol || _methodDeclaration.Body is null)
        {
            return;
        }

        foreach (var rule in rules)
        {
            foreach (var expr in _methodDeclaration.Body.DescendantNodes((s) => s.GetType() != typeof(LocalFunctionStatementSyntax)))
            {
                ExpressionProcessingResult result = rule.Process(_parent, methodSymbol, expr);

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

    private void SetAsyncIsNeeded(MethodNode method = null)
    {
        if (method is not null)
        {
            _methodInvocationAsynchronizationNeeded.Add(method);
        }

        if (_method.IsAsync || IsAsyncNeeded)
        {
            return;
        }

        IsAsyncNeeded = true;

        _overidedMethod?.SetAsyncIsNeeded();

        foreach (MethodNode implementedMethod in _implementedMethods)
        {
            implementedMethod.SetAsyncIsNeeded();
        }

        foreach (var caller in _invokedByMethods)
        {
            caller.SetAsyncIsNeeded(this);
        }

        foreach (var caller in _implemenetedByMethods)
        {
            caller.SetAsyncIsNeeded();
        }

        foreach (var caller in _overridedByMethods)
        {
            caller.SetAsyncIsNeeded();
        }
    }

    public bool NeedsAsynchronization => IsAsyncNeeded || _detectedIssues.Count > 0 || _methodInvocationAsynchronizationNeeded.Count > 0;

    internal void AsynchronizeMethod()
    {
        if (!NeedsAsynchronization || IsAsynchronized)
        {
            return;
        }

        Dictionary<SyntaxNode, SyntaxNode> nodeReplacements = [];

        foreach(var key in _nodeReplacements.Keys)
        {
            nodeReplacements.Add(key, _nodeReplacements[key]);
        }

        Dictionary<SyntaxToken, SyntaxToken> tokenReplacements = [];

        bool isChanged = false;

        if (!_method.Name.EndsWith("Async", StringComparison.InvariantCultureIgnoreCase) && !_method.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase))
        {
            var newMethodName = _method.Name + "Async";

            var newIdentifier = SyntaxFactory.Identifier(newMethodName)
                                            .WithLeadingTrivia(_methodDeclaration.Identifier.LeadingTrivia)
                                            .WithTrailingTrivia(_methodDeclaration.Identifier.TrailingTrivia);

            tokenReplacements.Add(_methodDeclaration.Identifier, newIdentifier);

            isChanged = true;
        }

        foreach (var detectedIssue in _detectedIssues)
        {
            var (oldNode, newNode) = detectedIssue.Rule.Fix(detectedIssue.Nodes);
            nodeReplacements.Add(oldNode, newNode);

            isChanged = true;
        }

        if (!IsTaskReturned)
        {
            var specType = "Task";

            if (!_method.ReturnType.Name.Equals("void", StringComparison.InvariantCultureIgnoreCase))
            {
                specType = $"{specType}<{_method.ReturnType.Name}>";
            }

            var newReturnType = SyntaxFactory.ParseTypeName(specType)
                .WithTrailingTrivia(_methodDeclaration.ReturnType.GetTrailingTrivia())
                .WithLeadingTrivia(_methodDeclaration.ReturnType.GetLeadingTrivia());

            nodeReplacements.Add(_methodDeclaration.ReturnType, newReturnType);

            isChanged = true;
        }

        if (_methodInvocationAsynchronizationNeeded.Count > 0 && _methodDeclaration.Body is not null)
        {
            var methodInvocations = _methodDeclaration.Body
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in methodInvocations)
            {
                var method = SymbolInfoStorage.Instance[invocation];

                if (method is null)
                {
                    continue;
                }

                if (!_methodInvocationAsynchronizationNeeded.Contains(method))
                {
                    continue;
                }

                var first = invocation.DescendantNodes().First();

                SyntaxNode updatedNode = (first) switch
                {
                    MemberAccessExpressionSyntax memeberAccess => memeberAccess.WithName(SyntaxFactory.IdentifierName(memeberAccess.Name.Identifier.Text + "Async")),
                    IdentifierNameSyntax identifierName => SyntaxFactory.IdentifierName(identifierName.Identifier.Text + "Async"),
                    _ => throw new NotImplementedException()
                };

                InvocationExpressionSyntax newInvocationNode = invocation.ReplaceNode(first, updatedNode)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(" "));
                AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(newInvocationNode)
                    .WithTrailingTrivia(invocation.GetTrailingTrivia())
                    .WithLeadingTrivia(invocation.GetLeadingTrivia());

                nodeReplacements.Add(invocation, awaitExpression);

                isChanged = true;
            }
        }

        var newMethodDeclaration = _methodDeclaration.ReplaceSyntax(nodeReplacements, tokenReplacements, null);

        if (_methodDeclaration.Body is not null && !newMethodDeclaration.Modifiers.Any(x => x.Text == "async"))
        {
            newMethodDeclaration = newMethodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }

        if (isChanged)
        {
            if (_parentMethod is not null)
            {
                _parentMethod.Replace(_methodDeclaration.Node, newMethodDeclaration.Node);
            }
            else
            {
                _parent.Parent.Replace(_methodDeclaration.Node, newMethodDeclaration.Node);
            }
        }
    }

    private readonly Dictionary<SyntaxNode, SyntaxNode> _nodeReplacements = [];
    private void Replace(SyntaxNode old, SyntaxNode @new)
    {
        _nodeReplacements.Add(old, @new);
    }

    public override bool Equals(object obj)
    {
        if (this is null && obj is null)
        {
            return true;
        }

        if (obj is null)
        {
            return false;
        }

        if (this is null)
        {
            return false;
        }

        if (obj is not MethodNode methodNode)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(Symbol, methodNode.Symbol);
    }

    public override int GetHashCode()
    {
        if (this is null || Symbol is null)
        {
            return 0;
        }

        return SymbolEqualityComparer.Default.GetHashCode(Symbol);
    }

    class MethodSignature
    {
        private readonly IMethodSymbol _method;
        public ITypeSymbol ReturnType => _method.ReturnType;
        public string Name => _method.Name;
        public ImmutableArray<IParameterSymbol> Params => _method.Parameters;

        public MethodSignature(IMethodSymbol method)
        {
            _method = method;
        }

        public override bool Equals(object obj)
        {
            if (obj is null && this is null)
            {
                return true;
            }

            if (obj is null || this is null)
            {
                return true;
            }

            if (obj is not MethodSignature otherSignature)
            {
                return false;
            }

            return otherSignature.Name.Equals(Name)
                && otherSignature.ReturnType.Equals(ReturnType, SymbolEqualityComparer.IncludeNullability)
                && otherSignature.Params.Length == Params.Length
                && otherSignature.Params.All(x => Params.FirstOrDefault(p => p.Equals(x, SymbolEqualityComparer.IncludeNullability)) is not null);
        }
    }

    abstract class MethodDeclarationSyntaxWrapperBase
    {
        public abstract SyntaxNode Node { get; }
        public abstract BlockSyntax Body { get; }
        public abstract SyntaxToken Identifier { get; }
        public abstract TypeSyntax ReturnType { get; }
        public abstract SyntaxTokenList Modifiers { get; }

        public abstract MethodDeclarationSyntaxWrapperBase ReplaceSyntax(Dictionary<SyntaxNode, SyntaxNode> nodeReplacements, Dictionary<SyntaxToken, SyntaxToken> tokenReplacements, Dictionary<SyntaxTrivia, SyntaxTrivia> triviaReplacements);

        public abstract MethodDeclarationSyntaxWrapperBase AddModifiers(SyntaxToken modifier);
    }

    class MethodDeclarationSyntaxWrapper : MethodDeclarationSyntaxWrapperBase
    {
        private readonly MethodDeclarationSyntax _methodDeclarationSyntax;

        public MethodDeclarationSyntaxWrapper(MethodDeclarationSyntax methodDeclarationSyntax)
        {
            _methodDeclarationSyntax = methodDeclarationSyntax;
        }

        public override SyntaxNode Node => _methodDeclarationSyntax;
        public override BlockSyntax Body => _methodDeclarationSyntax.Body;
        public override SyntaxToken Identifier => _methodDeclarationSyntax.Identifier;
        public override TypeSyntax ReturnType => _methodDeclarationSyntax.ReturnType;
        public override SyntaxTokenList Modifiers => _methodDeclarationSyntax.Modifiers;

        public override MethodDeclarationSyntaxWrapperBase AddModifiers(SyntaxToken modifier)
        {
            modifier = modifier.WithLeadingTrivia(ReturnType.GetLeadingTrivia());

            var newReturnType = ReturnType.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));

            MethodDeclarationSyntax newMethodDeclaration = _methodDeclarationSyntax
                .WithReturnType(newReturnType)
                .AddModifiers(modifier);

            return new MethodDeclarationSyntaxWrapper(newMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase ReplaceSyntax(Dictionary<SyntaxNode, SyntaxNode> nodeReplacements, Dictionary<SyntaxToken, SyntaxToken> tokenReplacements, Dictionary<SyntaxTrivia, SyntaxTrivia> triviaReplacements)
        {
            MethodDeclarationSyntax newMethodDeclaration = _methodDeclarationSyntax.ReplaceSyntax(
            nodeReplacements.Keys, (a, _) => nodeReplacements[a],
            tokenReplacements.Keys, (a, _) => tokenReplacements[a],
            triviaReplacements?.Keys, (a, _) => triviaReplacements[a]);

            return new MethodDeclarationSyntaxWrapper(newMethodDeclaration);
        }
    }

    class LocalMethodDeclarationSyntaxWrapper : MethodDeclarationSyntaxWrapperBase
    {
        private readonly LocalFunctionStatementSyntax _localFunctionStatementSyntax;

        public LocalMethodDeclarationSyntaxWrapper(LocalFunctionStatementSyntax localFunctionStatementSyntax)
        {
            _localFunctionStatementSyntax = localFunctionStatementSyntax;
        }

        public override SyntaxNode Node => _localFunctionStatementSyntax;
        public override BlockSyntax Body => _localFunctionStatementSyntax.Body;
        public override SyntaxToken Identifier => _localFunctionStatementSyntax.Identifier;
        public override TypeSyntax ReturnType => _localFunctionStatementSyntax.ReturnType;
        public override SyntaxTokenList Modifiers => _localFunctionStatementSyntax.Modifiers;

        public override MethodDeclarationSyntaxWrapperBase AddModifiers(SyntaxToken modifier)
        {
            modifier = modifier.WithLeadingTrivia(ReturnType.GetLeadingTrivia());

            var newReturnType = ReturnType.WithLeadingTrivia(SyntaxFactory.Whitespace(" "));

            LocalFunctionStatementSyntax newLocalMethodDeclaration = _localFunctionStatementSyntax
                .WithReturnType(newReturnType)
                .AddModifiers(modifier);

            return new LocalMethodDeclarationSyntaxWrapper(newLocalMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase ReplaceSyntax(Dictionary<SyntaxNode, SyntaxNode> nodeReplacements, Dictionary<SyntaxToken, SyntaxToken> tokenReplacements, Dictionary<SyntaxTrivia, SyntaxTrivia> triviaReplacements)
        {
            LocalFunctionStatementSyntax newLocalMethodDeclaration = _localFunctionStatementSyntax.ReplaceSyntax(
            nodeReplacements.Keys, (a, _) => nodeReplacements[a],
            tokenReplacements.Keys, (a, _) => tokenReplacements[a],
            triviaReplacements?.Keys, (a, _) => triviaReplacements[a]);

            return new LocalMethodDeclarationSyntaxWrapper(newLocalMethodDeclaration);
        }
    }

    class DetectedIssue
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

