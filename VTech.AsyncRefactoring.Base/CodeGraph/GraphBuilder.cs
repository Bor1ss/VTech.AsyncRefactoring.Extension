using Microsoft.CodeAnalysis.Operations;

using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring;

//https://joshvarty.com/learn-roslyn-now/
//https://ikoshelev.azurewebsites.net/search/id/19/Roslyn-beyond-'Hello-world'-06-One-off-code-changes-with-Roslyn

public sealed class GraphBuilder : CSharpSyntaxWalker
{
    private readonly GraphBuilderOptions _options;

    private readonly GraphBuildingContext _graphBuildingContext = new();

    public GraphBuilder(GraphBuilderOptions options)
    {
        _options = options;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        ISymbol symbol = GetSymbol(node, _options.Document.SemanticModel);
        ClassNode classNode = new(symbol, node, _options.Document);
        _options.Document.AddTypeDeclaration(classNode);

        _graphBuildingContext.Next(classNode, node, base.VisitClassDeclaration);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var symbol = GetSymbol(node, _options.Document.SemanticModel);
        InterfaceNode interfaceNode = new(symbol, node, _options.Document);
        _options.Document.AddTypeDeclaration(interfaceNode);

        _graphBuildingContext.Next(interfaceNode, node, base.VisitInterfaceDeclaration);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        var symbol = GetSymbol(node, _options.Document.SemanticModel);
        StructNode structNode = new(symbol, node, _options.Document);
        _options.Document.AddTypeDeclaration(structNode);

        _graphBuildingContext.Next(structNode, node, base.VisitStructDeclaration);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        var symbol = GetSymbol(node, _options.Document.SemanticModel);
        RecordNode recordNode = new(symbol, node, _options.Document);
        _options.Document.AddTypeDeclaration(recordNode);

        _graphBuildingContext.Next(recordNode, node, base.VisitRecordDeclaration);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        MethodNode methodNode = new(node, methodSymbol as IMethodSymbol, _graphBuildingContext.Type);

        var descendants = node.DescendantNodes();
        var methodInvocations = descendants.OfType<InvocationExpressionSyntax>();

        foreach (var invocation in methodInvocations)
        {
            var invocationSymbol = GetSymbol(invocation);
            if (invocationSymbol == null) continue;

            methodNode.AddInvocation(invocationSymbol);

            _options.SymbolInfoStorage.Set(invocation, invocationSymbol);
        }

        var identifiers = descendants.OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifiers)
        {
            var identifierSymbol = GetSymbol(identifier);
            if (identifierSymbol == null) continue;

            methodNode.AddInvocation(identifierSymbol);

            _options.SymbolInfoStorage.Set(identifier, identifierSymbol);
        }

        _graphBuildingContext.Next(methodNode, node, base.VisitMethodDeclaration);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        MethodNode methodNode = new(node, methodSymbol as IMethodSymbol, _graphBuildingContext.Type, _graphBuildingContext.Method);

        var descendants = node.DescendantNodes();
        var methodInvocations = descendants.OfType<InvocationExpressionSyntax>();

        foreach (var invocation in methodInvocations)
        {
            var invocationSymbol = GetSymbol(invocation);
            if (invocationSymbol == null) continue;

            methodNode.AddInvocation(invocationSymbol);

            _options.SymbolInfoStorage.Set(invocation, invocationSymbol);
        }

        var identifiers = descendants.OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifiers)
        {
            var identifierSymbol = GetSymbol(identifier);
            if (identifierSymbol == null) continue;

            methodNode.AddInvocation(identifierSymbol);

            _options.SymbolInfoStorage.Set(identifier, identifierSymbol);
        }

        _graphBuildingContext.Next(methodNode, node, base.VisitLocalFunctionStatement);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        MethodNode method = new(node, methodSymbol as IMethodSymbol, _graphBuildingContext.Type, _graphBuildingContext.Method);

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

        _graphBuildingContext.Next(method, node, base.VisitSimpleLambdaExpression);
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        var methodSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (methodSymbol == null) return;

        MethodNode method = new(node, methodSymbol as IMethodSymbol, _graphBuildingContext.Type, _graphBuildingContext.Method);

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

        _graphBuildingContext.Next(method, node, base.VisitParenthesizedLambdaExpression);
    }

    private ISymbol GetSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        try
        {
            return node.GetType().IsSubclassOf(typeof(MemberDeclarationSyntax)) || node.GetType() == typeof(VariableDeclaratorSyntax) || node.GetType() == typeof(LocalFunctionStatementSyntax)
            ? semanticModel.GetDeclaredSymbol(node)
            : semanticModel.GetSymbolInfo(node).Symbol;
        }
        catch (Exception ex)
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

        foreach (var semanticModel in _options.AllSemanticModels)
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
        var variableSymbol = GetSymbol(node, _options.Document.SemanticModel);

        if (variableSymbol == null) return;

        if(variableSymbol is IFieldSymbol fieldSymbol && fieldSymbol.IsConst)
        {
            return;
        }

        IdentifierNameSyntax identifierName = node.Parent.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault();

        if(identifierName is null)
        {
            return;
        }

        SymbolInfo identifierSymbol = _options.Document.SemanticModel.GetSymbolInfo(identifierName);
        TypeInfo typeInfo = _options.Document.SemanticModel.GetTypeInfo(identifierName);

        //EqualsValueClauseSyntax variableAssignment = node.DescendantNodes().OfType<EqualsValueClauseSyntax>().FirstOrDefault();
        //if (variableAssignment is null)
        //{
        //    SyntaxNode parentNode = _parentMethod?.Node ?? _baseNode.TypeDeclarationSyntax;
        //    IEnumerable<AssignmentExpressionSyntax> assignments = parentNode.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        //    foreach (AssignmentExpressionSyntax assignmentExpression in assignments)
        //    {
        //        IdentifierNameSyntax subIdentifier = assignmentExpression.DescendantNodes()
        //            .OfType<IdentifierNameSyntax>()
        //            .FirstOrDefault(x => x.Identifier.Text.Equals(identifierName.Identifier.Text));

        //        SymbolInfo subIdentifierSymbol = _options.Document.SemanticModel.GetSymbolInfo(subIdentifier);
        //        if(SymbolEqualityComparer.Default.Equals(subIdentifierSymbol.Symbol, identifierSymbol.Symbol))
        //        {
        //            variableAssignment = 
        //        }
        //    }
        //}

        VariableDeclarationNode variableDeclarationNode = new(node, variableSymbol, typeInfo, _graphBuildingContext.Type, _graphBuildingContext.Method);

        _graphBuildingContext.Next(variableDeclarationNode, node, base.VisitVariableDeclarator);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        base.VisitFieldDeclaration(node);
    }

    class GraphBuildingContext
    {
        private BaseTypeDeclarationNode _typeNode;
        private MethodNode _method;
        private VariableDeclarationNode _variableNode;

        private readonly Stack<IDeclarationParent> _parents = [];

        public BaseTypeDeclarationNode Type => _typeNode;
        public MethodNode Method => _method;
        public VariableDeclarationNode Variable => _variableNode;

        public void Next<T>(BaseTypeDeclarationNode typeDeclarationNode, T syntaxNode, Action<T> action)
        {
            BaseTypeDeclarationNode prevBaseNode = _typeNode;
            MethodNode prevMethodNode = _method;
            VariableDeclarationNode prevVariableNode = _variableNode;

            _typeNode = typeDeclarationNode;
            _method = null;
            _variableNode = null;

            _parents.Push(typeDeclarationNode);

            action(syntaxNode);

            _parents.Pop();

            _variableNode = prevVariableNode;
            _method = prevMethodNode;
            _typeNode = prevBaseNode;
        }

        public void Next<T>(MethodNode method, T syntaxNode, Action<T> action)
        {
            _parents.Peek().AddMethod(method);

            MethodNode prevMethodNode = _method;
            VariableDeclarationNode prevVariableNode = _variableNode;

            _method = method;
            _variableNode = null;

            _parents.Push(method);

            action(syntaxNode);

            _parents.Pop();

            _variableNode = prevVariableNode;
            _method = prevMethodNode;
        }

        public void Next<T>(VariableDeclarationNode variableDeclarationNode, T syntaxNode, Action<T> action)
        {
            _parents.Peek().AddVariableDeclaration(variableDeclarationNode);

            VariableDeclarationNode prevVariableNode = _variableNode;

            _variableNode = variableDeclarationNode;

            _parents.Push(variableDeclarationNode);

            action(syntaxNode);

            _parents.Pop();

            _variableNode = prevVariableNode;
        }
    }
}

