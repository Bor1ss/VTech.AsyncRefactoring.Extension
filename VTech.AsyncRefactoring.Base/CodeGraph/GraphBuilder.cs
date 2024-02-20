using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring;

//https://joshvarty.com/learn-roslyn-now/
//https://ikoshelev.azurewebsites.net/search/id/19/Roslyn-beyond-'Hello-world'-06-One-off-code-changes-with-Roslyn

public sealed class GraphBuilder : CSharpSyntaxWalker
{
    private readonly GraphBuilderOptions options;

    private BaseTypeDeclarationNode? _baseNode;
    private MethodNode? _parentMethod;

    public GraphBuilder(GraphBuilderOptions options)
    {
        this.options = options;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = GetSymbol(node);
        _baseNode = new ClassNode(symbol, node, options.Document);
        options.Document.AddTypeDeclaration(_baseNode);

        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var symbol = GetSymbol(node);
        _baseNode = new InterfaceNode(symbol, node, options.Document);
        options.Document.AddTypeDeclaration(_baseNode);

        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var symbol = GetSymbol(node);
        _baseNode = new StructNode(symbol, node, options.Document);
        options.Document.AddTypeDeclaration(_baseNode);

        base.VisitStructDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var symbol = GetSymbol(node);
        _baseNode = new RecordNode(symbol, node, options.Document);
        options.Document.AddTypeDeclaration(_baseNode);

        base.VisitRecordDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var methodSymbol = GetSymbol(node);

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

            SymbolInfoStorage.Instance.Set(invocation, invocationSymbol);

            /*
              //todo: callers to the method
              var callers = SymbolFinder
                    .FindCallersAsync(symbolRef.Symbol, _document.ProjectNode.Solution)
                    .Result
                    .Select(c => c.CallingSymbol.GetFullyQualifiedName())
                    .GroupBy(fqn => fqn);
             
             */
        }

        _parentMethod = method;
        base.VisitMethodDeclaration(node);
        _parentMethod = null;
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var methodSymbol = GetSymbol(node);

        if (methodSymbol == null) return;

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

            SymbolInfoStorage.Instance.Set(invocation, invocationSymbol);

            /*
              //todo: callers to the method
              var callers = SymbolFinder
                    .FindCallersAsync(symbolRef.Symbol, _document.ProjectNode.Solution)
                    .Result
                    .Select(c => c.CallingSymbol.GetFullyQualifiedName())
                    .GroupBy(fqn => fqn);
             
             */
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

    private ISymbol GetSymbol(SyntaxNode node)
    {
        Func<SemanticModel, ISymbol?> symbolGetter = node.GetType().IsSubclassOf(typeof(MemberDeclarationSyntax)) || node.GetType() == typeof(VariableDeclaratorSyntax) || node.GetType() == typeof(LocalFunctionStatementSyntax)
            ? (m) => m.GetDeclaredSymbol(node)
            : (m) => m.GetSymbolInfo(node).Symbol;

        foreach(var semanticModel in options.AllSemanticModels)
        {
            try
            {
                var symbol = symbolGetter(semanticModel);

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
        var t = options.Document.SemanticModel.GetTypeInfo(node.Initializer.Value);
        base.VisitVariableDeclarator(node);
    }
}

