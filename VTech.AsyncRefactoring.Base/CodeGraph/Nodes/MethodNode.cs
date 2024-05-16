using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;

using Microsoft.CodeAnalysis;

using VTech.AsyncRefactoring.Base.Rules;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

[DebuggerDisplay("{_methodDeclaration.Identifier.Text}")]
public class MethodNode
{
    private readonly BaseTypeDeclarationNode _parent;
    private readonly MethodDeclarationSyntaxWrapperBase _methodDeclaration;
    private readonly IMethodSymbol _method;

    private bool _isThridPartyApiImplemented = false;
    /// <summary>
    /// True if method with the same signature, but sync/async exists
    /// </summary>
    private bool _duplicateMethodExist = false;

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
    public Location Location => _methodDeclaration.Node.GetLocation();

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
    private void AddInvoker(MethodNode method)
    {
        _invokedByMethods.Add(method);
    }

    internal void CompleteReferences(Dictionary<ISymbol, MethodNode> symbolMethodMap)
    {
        if (_method.IsOverride && _method.OverriddenMethod is null)
        {
            _isThridPartyApiImplemented = true;
        }
        else if (_method.OverriddenMethod is not null)
        {
            if (symbolMethodMap.TryGetValue(_method.OverriddenMethod, out var overidedMethod))
            {
                _overidedMethod = overidedMethod;
                _overidedMethod?.AddOverrider(this);
            }
            else
            {
                _isThridPartyApiImplemented = true;
            }
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

        if(_duplicateMethodExist)
        {
            return;
        }

        MethodNode duplicateMethod = _parent.Methods.SingleOrDefault(x => x.Signature.IsAsyncDuplicate(Signature));
        if (duplicateMethod is not null)
        {
            _duplicateMethodExist = true;
            duplicateMethod._duplicateMethodExist = true;
        }
    }


    internal void DetectIssues(IReadOnlyList<IRule> rules)
    {
        if (_method is not IMethodSymbol methodSymbol || _methodDeclaration.Body is null || _isThridPartyApiImplemented && !IsTaskReturned)
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

        if (_method.IsAsync || IsTaskReturned || IsAsyncNeeded || _isThridPartyApiImplemented || _duplicateMethodExist)
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

    internal void PrepareFixes(SymbolInfoStorage symbolInfoStorage)
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

        Dictionary<SyntaxToken, SyntaxToken> tokenReplacements = [];

        bool isChanged = false;
        bool isAsyncAfterChanges = IsTaskReturned;

        if (!_duplicateMethodExist && !IsTaskReturned && !_isThridPartyApiImplemented && !_method.Name.EndsWith("Async", StringComparison.InvariantCultureIgnoreCase) && !_method.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase))
        {
            var newMethodName = _method.Name + "Async";

            var newIdentifier = SyntaxFactory.Identifier(newMethodName)
                                            .WithLeadingTrivia(_methodDeclaration.Identifier.LeadingTrivia)
                                            .WithTrailingTrivia(_methodDeclaration.Identifier.TrailingTrivia);

            tokenReplacements.Add(_methodDeclaration.Identifier, newIdentifier);

            isChanged = true;
        }

        bool isEnumerableReturned = false;
        if (!_duplicateMethodExist && !_isThridPartyApiImplemented && !IsTaskReturned)
        {
            var specType = "Task";

            if (!_method.ReturnType.Name.Equals("void", StringComparison.InvariantCultureIgnoreCase))
            {
                bool isFullyQualified = _methodDeclaration.ReturnType.IsKind(SyntaxKind.QualifiedName);
                specType = $"{specType}<{_methodDeclaration.ReturnType}>";
                isEnumerableReturned = specType.Contains("IEnumerable<");
            }

            var newReturnType = SyntaxFactory.ParseTypeName(specType)
                .WithTrailingTrivia(_methodDeclaration.ReturnType.GetTrailingTrivia())
                .WithLeadingTrivia(_methodDeclaration.ReturnType.GetLeadingTrivia());

            nodeReplacements.Add(_methodDeclaration.ReturnType, newReturnType);

            isAsyncAfterChanges = true;
            isChanged = true;
        }

        if (isAsyncAfterChanges)
        {
            foreach (var detectedIssue in _detectedIssues)
            {
                var (oldNode, newNode) = detectedIssue.Rule.Fix(detectedIssue.Nodes);
                nodeReplacements.Add(oldNode, newNode);

                isChanged = true;
            }
        }

        if (_methodInvocationAsynchronizationNeeded.Count > 0 && _methodDeclaration.Body is not null)
        {
            var methodInvocations = _methodDeclaration.Body
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in methodInvocations)
            {
                MethodNode method = symbolInfoStorage[invocation];

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

                if (!isAsyncAfterChanges)
                {
                    MemberAccessExpressionSyntax awaiterExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newInvocationNode,
                        SyntaxFactory.IdentifierName("GetAwaiter"));

                    MemberAccessExpressionSyntax getResultInvocation = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        awaiterExpression,
                        SyntaxFactory.IdentifierName("GetResult"))
                    .WithTrailingTrivia(invocation.GetTrailingTrivia())
                    .WithLeadingTrivia(invocation.GetLeadingTrivia()); ;

                    nodeReplacements.Add(invocation, getResultInvocation);
                }
                else
                {
                    AwaitExpressionSyntax awaitExpression = SyntaxFactory.AwaitExpression(newInvocationNode)
                        .WithTrailingTrivia(invocation.GetTrailingTrivia())
                        .WithLeadingTrivia(invocation.GetLeadingTrivia());

                    nodeReplacements.Add(invocation, awaitExpression);
                }

                isChanged = true;
            }
        }

        var newMethodDeclaration = _methodDeclaration.ReplaceSyntax(nodeReplacements, tokenReplacements, null);

        if (!_duplicateMethodExist && !_isThridPartyApiImplemented && isEnumerableReturned && newMethodDeclaration.Body is not null)
        {
            newMethodDeclaration = ReplaceYield(newMethodDeclaration);
        }

        if (!_duplicateMethodExist && !_isThridPartyApiImplemented && _methodDeclaration.Body is not null && !newMethodDeclaration.Modifiers.Any(x => x.Text == "async"))
        {
            newMethodDeclaration = newMethodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
        }
        //Thread.Sleep(1000);
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

    private MethodDeclarationSyntaxWrapperBase ReplaceYield(MethodDeclarationSyntaxWrapperBase methodDeclaration)
    {
        List<YieldStatementSyntax> yieldStatements = methodDeclaration.Body.DescendantNodes((s) => s.GetType() != typeof(LocalFunctionStatementSyntax)).OfType<YieldStatementSyntax>().ToList();
        if (yieldStatements.Count == 0)
        {
            return methodDeclaration;
        }

        string declaredListName = $"{_method.Name}Result";
        TypeArgumentListSyntax typeArgumentList = methodDeclaration.Node.DescendantNodes().OfType<TypeArgumentListSyntax>().Skip(1).First();
        GenericNameSyntax resultListTypeSyntax = SyntaxFactory.GenericName(SyntaxFactory.Identifier("List"), typeArgumentList);
        LocalDeclarationStatementSyntax listDeclarationSyntax = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(resultListTypeSyntax, SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(declaredListName).WithTrailingTrivia(SyntaxFactory.Space), null,
                                    SyntaxFactory.EqualsValueClause(
                                            SyntaxFactory.ObjectCreationExpression(SyntaxFactory.Token(SyntaxKind.NewKeyword).WithTrailingTrivia(SyntaxFactory.Space), resultListTypeSyntax, SyntaxFactory.ArgumentList(), null).WithLeadingTrivia(SyntaxFactory.Space)
                                        )
                                )
                            )
                    )
            ).WithTrailingTrivia(SyntaxFactory.LineFeed);

        SyntaxNode returnStatementSyntax = SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(declaredListName).WithLeadingTrivia(SyntaxFactory.Space)).WithTrailingTrivia(SyntaxFactory.LineFeed);

        SyntaxNode firstNode = methodDeclaration.Body.ChildNodes().First();
        listDeclarationSyntax = listDeclarationSyntax.WithLeadingTrivia(firstNode.GetLeadingTrivia());

        methodDeclaration = methodDeclaration
            .InsertChildBefore(firstNode, [listDeclarationSyntax]);

        SyntaxNode lastNode = methodDeclaration.Body.ChildNodes().Last();
        returnStatementSyntax = returnStatementSyntax.WithLeadingTrivia(lastNode.GetLeadingTrivia());

        methodDeclaration = methodDeclaration
            .InsertChildAfter(lastNode, [returnStatementSyntax]);

        yieldStatements = methodDeclaration.Body.DescendantNodes((s) => s.GetType() != typeof(LocalFunctionStatementSyntax)).OfType<YieldStatementSyntax>().ToList();

        Dictionary<SyntaxNode, SyntaxNode> nodeReplacements = [];

        foreach (YieldStatementSyntax yieldStatement in yieldStatements)
        {
            if (yieldStatement.IsKind(SyntaxKind.YieldBreakStatement))
            {
                nodeReplacements.Add(yieldStatement, SyntaxFactory.BreakStatement());

                continue;
            }

            ExpressionSyntax expressionElement = yieldStatement.DescendantNodes().ElementAt(0) as ExpressionSyntax;
            ArgumentSyntax argumentSyntax = null;
            if (expressionElement is not null)
            {
                argumentSyntax = SyntaxFactory.Argument(expressionElement);
            }
            else
            {
                Debugger.Break();
            }

            ExpressionStatementSyntax expressionStatement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(declaredListName),
                            SyntaxFactory.IdentifierName("Add")
                        ),
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                argumentSyntax
                            )
                        )
                    )
                )
                .WithLeadingTrivia(yieldStatement.GetLeadingTrivia())
                .WithTrailingTrivia(yieldStatement.GetTrailingTrivia());

            nodeReplacements.Add(yieldStatement, expressionStatement);
        }

        return methodDeclaration.ReplaceSyntax(nodeReplacements, null, null);
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

        public bool IsAsyncDuplicate(MethodSignature otherSignature)
        {
            if (otherSignature is null && this is null)
            {
                return true;
            }

            if (otherSignature is null || this is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(_method, otherSignature._method)
                || SymbolEqualityComparer.Default.Equals(ReturnType, otherSignature.ReturnType))
            {
                return false;
            }

            bool isOfSameName = otherSignature.Name.Equals(Name)
                || otherSignature.Name.Equals(Name.Replace("Async", ""))
                || otherSignature.Name.Replace("Async", "").Equals(Name);

            if (!isOfSameName)
            {
                return false;
            }

            if (Params.Length == 0 && otherSignature.Params.Length == 0)
            {
                return true;
            }

            int paramsLengthDif = Math.Abs(Params.Length - otherSignature.Params.Length);
            if (paramsLengthDif > 1)
            {
                return false;
            }

            var nonCorrespondingParams = Params.Except(otherSignature.Params, SymbolEqualityComparer.Default)
                .OfType<IParameterSymbol>()
                .ToList();

            if (nonCorrespondingParams.Count > 1)
            {
                return false;
            }
            if (nonCorrespondingParams.Count == 0)
            {
                return true;
            }

            IParameterSymbol nonCorrespondingParam = nonCorrespondingParams[0];

            string @namespace = nonCorrespondingParam.Type?.ContainingNamespace?.ToString();
            string type = nonCorrespondingParam.Type?.Name;
            return type == "CancellationToken" && @namespace == "System.Threading";
        }

        public override bool Equals(object obj)
        {
            if (obj is null && this is null)
            {
                return true;
            }

            if (obj is null || this is null)
            {
                return false;
            }

            if (obj is not MethodSignature otherSignature)
            {
                return false;
            }

            bool baseEquals = otherSignature.Name.Equals(Name)
                && otherSignature.ReturnType.Equals(ReturnType, SymbolEqualityComparer.IncludeNullability)
                && otherSignature.Params.Length == Params.Length;

            if (!baseEquals)
            {
                return false;
            }

            for (int i = 0; i < Params.Length; i++)
            {
                var paramType = Params[i].Type;
                var otherParamType = otherSignature.Params[i].Type;

                if (SymbolEqualityComparer.Default.Equals(paramType, otherParamType))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hashCode = -1312838752;
            hashCode = hashCode * -1521134295 + EqualityComparer<ITypeSymbol>.Default.GetHashCode(ReturnType);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + Params.GetHashCode();
            return hashCode;
        }
    }

    class ParameterSymbolEqulaityComparer : IEqualityComparer<IParameterSymbol>
    {
        public bool Equals(IParameterSymbol x, IParameterSymbol y)
        {
            if (x is null && y is null)
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            bool typeEquals = SymbolEqualityComparer.Default.Equals(x.Type, y.Type);
            if(!typeEquals)
            {
                return false;
            }

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase); 
        }

        public int GetHashCode(IParameterSymbol obj)
        {
            return obj.Type.GetHashCode();
        }
    }

    abstract class MethodDeclarationSyntaxWrapperBase
    {
        public abstract SyntaxNode Node { get; }
        public abstract CSharpSyntaxNode Body { get; }
        public abstract SyntaxToken Identifier { get; }
        public abstract TypeSyntax ReturnType { get; }
        public abstract SyntaxTokenList Modifiers { get; }

        public abstract MethodDeclarationSyntaxWrapperBase ReplaceSyntax(Dictionary<SyntaxNode, SyntaxNode> nodeReplacements, Dictionary<SyntaxToken, SyntaxToken> tokenReplacements, Dictionary<SyntaxTrivia, SyntaxTrivia> triviaReplacements);

        public abstract MethodDeclarationSyntaxWrapperBase AddModifiers(SyntaxToken modifier);

        public abstract MethodDeclarationSyntaxWrapperBase InsertChildBefore(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes);
        public abstract MethodDeclarationSyntaxWrapperBase InsertChildAfter(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes);
    }

    class MethodDeclarationSyntaxWrapper : MethodDeclarationSyntaxWrapperBase
    {
        private readonly MethodDeclarationSyntax _methodDeclarationSyntax;

        public MethodDeclarationSyntaxWrapper(MethodDeclarationSyntax methodDeclarationSyntax)
        {
            _methodDeclarationSyntax = methodDeclarationSyntax;
        }

        public override SyntaxNode Node => _methodDeclarationSyntax;
        public override CSharpSyntaxNode Body => _methodDeclarationSyntax.Body is not null
            ? _methodDeclarationSyntax.Body
            : _methodDeclarationSyntax.ExpressionBody;
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

        public override MethodDeclarationSyntaxWrapperBase InsertChildBefore(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes)
        {
            MethodDeclarationSyntax newMethodDeclaration = _methodDeclarationSyntax.InsertNodesBefore(nodeInList, newNodes);

            return new MethodDeclarationSyntaxWrapper(newMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase InsertChildAfter(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes)
        {
            MethodDeclarationSyntax newMethodDeclaration = _methodDeclarationSyntax.InsertNodesAfter(nodeInList, newNodes);

            return new MethodDeclarationSyntaxWrapper(newMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase ReplaceSyntax(Dictionary<SyntaxNode, SyntaxNode> nodeReplacements, Dictionary<SyntaxToken, SyntaxToken> tokenReplacements, Dictionary<SyntaxTrivia, SyntaxTrivia> triviaReplacements)
        {
            MethodDeclarationSyntax newMethodDeclaration = _methodDeclarationSyntax.ReplaceSyntax(
            nodeReplacements?.Keys, (a, _) => nodeReplacements[a],
            tokenReplacements?.Keys, (a, _) => tokenReplacements[a],
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
        public override CSharpSyntaxNode Body => _localFunctionStatementSyntax.Body is not null
            ? _localFunctionStatementSyntax.Body
            : _localFunctionStatementSyntax.ExpressionBody;
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
            nodeReplacements?.Keys, (a, _) => nodeReplacements[a],
            tokenReplacements?.Keys, (a, _) => tokenReplacements[a],
            triviaReplacements?.Keys, (a, _) => triviaReplacements[a]);

            return new LocalMethodDeclarationSyntaxWrapper(newLocalMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase InsertChildBefore(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes)
        {
            LocalFunctionStatementSyntax newLocalMethodDeclaration = _localFunctionStatementSyntax.InsertNodesBefore(nodeInList, newNodes);

            return new LocalMethodDeclarationSyntaxWrapper(newLocalMethodDeclaration);
        }

        public override MethodDeclarationSyntaxWrapperBase InsertChildAfter(SyntaxNode nodeInList, IEnumerable<SyntaxNode> newNodes)
        {
            LocalFunctionStatementSyntax newLocalMethodDeclaration = _localFunctionStatementSyntax.InsertNodesAfter(nodeInList, newNodes);

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

