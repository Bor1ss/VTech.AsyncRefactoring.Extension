using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring;

//https://joshvarty.com/learn-roslyn-now/
//https://ikoshelev.azurewebsites.net/search/id/19/Roslyn-beyond-'Hello-world'-06-One-off-code-changes-with-Roslyn

public sealed class GraphBuilder : CSharpSyntaxWalker
{
    private readonly GraphBuilderOptions _options;

    private BaseTypeDeclarationNode _baseNode;
    private MethodNode _parentMethod;

    public GraphBuilder(GraphBuilderOptions options)
    {
        _options = options;
    }

    private void OnNext<T>(BaseTypeDeclarationNode newBase, T node, Action<T> next)
    {
        _options.Document.AddTypeDeclaration(newBase);
        BaseTypeDeclarationNode oldBase = _baseNode;
        _baseNode = newBase;
        next(node);
        _baseNode = oldBase;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        ClassNode classNode = new (symbol, node, _options.Document);

        OnNext(classNode, node, base.VisitClassDeclaration);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        InterfaceNode interfaceNode = new (symbol, node, _options.Document);

        OnNext(interfaceNode, node, base.VisitInterfaceDeclaration);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        StructNode structNode = new (symbol, node, _options.Document);

        OnNext(structNode, node, base.VisitStructDeclaration);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        RecordNode recordNode = new (symbol, node, _options.Document);

        OnNext(recordNode, node, base.VisitRecordDeclaration);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        ISymbol methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        MethodNode method = new(node, methodSymbol as IMethodSymbol, _baseNode!);
        _baseNode!.AddMethod(method);

        var descendants = node.DescendantNodes();
        var methodInvocations = descendants.OfType<InvocationExpressionSyntax>();

        foreach (var invocation in methodInvocations)
        {
            var invocationSymbol = GetSymbol(invocation);
            if (invocationSymbol == null) continue;

            method.AddInvocation(invocationSymbol);

            _options.SymbolInfoStorage.Set(invocation, invocationSymbol);
        }

        var identifiers = descendants.OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifiers)
        {
            var identifierSymbol = GetSymbol(identifier);
            if (identifierSymbol == null) continue;

            method.AddInvocation(identifierSymbol);

            _options.SymbolInfoStorage.Set(identifier, identifierSymbol);
        }

        _parentMethod = method;
        base.VisitMethodDeclaration(node);
        _parentMethod = null;
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        //program.cs file with top level statements
        if(_parentMethod is null && _baseNode is null)
        {
            _baseNode = new ClassNode(null, null, _options.Document);
            _options.Document.AddTypeDeclaration(_baseNode);
            _parentMethod = new MethodNode(null, null, _baseNode);
            _baseNode.AddMethod(_parentMethod);
        }

        MethodNode method = new(node, methodSymbol as IMethodSymbol, _baseNode!, _parentMethod);
        _baseNode!.AddMethod(method);
        _parentMethod.AddInternalMethod(method);

        var descendants = node.DescendantNodes();
        var methodInvocations = descendants.OfType<InvocationExpressionSyntax>();

        foreach (var invocation in methodInvocations)
        {
            var invocationSymbol = GetSymbol(invocation);
            if (invocationSymbol == null) continue;

            method.AddInvocation(invocationSymbol);

            _options.SymbolInfoStorage.Set(invocation, invocationSymbol);
        }

        MethodNode previousParent = _parentMethod;
        _parentMethod = method;
        base.VisitLocalFunctionStatement(node);
        _parentMethod = previousParent;
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        base.VisitSimpleLambdaExpression(node);
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        base.VisitParenthesizedLambdaExpression(node);
    }

    private ISymbol GetSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        try
        {
            return node.GetType().IsSubclassOf(typeof(MemberDeclarationSyntax)) || node.GetType() == typeof(VariableDeclaratorSyntax) || node.GetType() == typeof(LocalFunctionStatementSyntax)
            ? semanticModel.GetDeclaredSymbol(node)
            : semanticModel.GetSymbolInfo(node).Symbol;
        }
        catch(Exception ex)
        {
            return null;
        }
    }

    private ISymbol GetSymbol(SyntaxNode node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        if(symbol is not null)
        {
            return symbol;
        }

        Func<SemanticModel, ISymbol> symbolGetter = node.GetType().IsSubclassOf(typeof(MemberDeclarationSyntax)) || node.GetType() == typeof(VariableDeclaratorSyntax) || node.GetType() == typeof(LocalFunctionStatementSyntax)
            ? (m) => m.GetDeclaredSymbol(node)
            : (m) => m.GetSymbolInfo(node).Symbol;

        string name = string.Empty;
        if (node is MemberAccessExpressionSyntax memberAccessExpression)
        {
            name = memberAccessExpression.Name.Identifier.ValueText;
        }
        else if (node is MemberBindingExpressionSyntax memberBindingExpression)
        {
            name = memberBindingExpression.Name.Identifier.ValueText;
        }
        else if (node is IdentifierNameSyntax identifierNameSyntax)
        {
            name = identifierNameSyntax.Identifier.ValueText;
        }
        else if (node is GenericNameSyntax genericName)
        {
            name = genericName.Identifier.ValueText;
        }
        else
        {
            //Debugger.Break();
            return null;
        }

        if(!_options.MethodSemanticModelsMap.TryGetValue(name, out var methodSemanticModels))
        {
            return null;
        }

        foreach (var semanticModel in methodSemanticModels)
        {
            try
            {
                symbol = symbolGetter(semanticModel);

                if (symbol is null)
                {
                    continue;
                }

                return symbol;
            }
            catch { }
        }

        return null;
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        //var t = _options.Document.SemanticModel.GetTypeInfo(node.Initializer.Value);
        base.VisitVariableDeclarator(node);
    }
}

