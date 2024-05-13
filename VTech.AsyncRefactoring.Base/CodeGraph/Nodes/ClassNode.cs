namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public abstract class BaseTypeDeclarationNode : IDeclarationParent
{
    protected readonly ISymbol _symbol;
    protected readonly TypeDeclarationSyntax _typeDeclarationSyntax;
    protected readonly DocumentNode _parent;
    protected readonly List<MethodNode> _methods = [];
    protected readonly List<BaseTypeDeclarationNode> _inherits = [];
    protected readonly List<BaseTypeDeclarationNode> _inheritedBy = [];
    protected readonly List<INamedTypeSymbol> _bases = [];
    protected readonly List<VariableDeclarationNode> _variables = [];

    protected BaseTypeDeclarationNode(ISymbol symbol, TypeDeclarationSyntax typeDeclarationSyntax, DocumentNode parent)
    {
        _symbol = symbol;
        _typeDeclarationSyntax = typeDeclarationSyntax;
        _parent = parent;

        if (symbol is ITypeSymbol typeSymbol)
        {
            if (typeSymbol.Interfaces.Any())
            {
                _bases.AddRange(typeSymbol.Interfaces);
            }

            if (typeSymbol.BaseType is not null)
            {
                _bases.Add(typeSymbol.BaseType!);
            }
        }
    }

    public string Id => _symbol.Name;
    public IReadOnlyList<MethodNode> Methods => _methods;
    public IReadOnlyList<VariableDeclarationNode> Variables => _variables;
    internal IReadOnlyList<BaseTypeDeclarationNode> Bases => _inherits;
    internal DocumentNode Parent => _parent;
    internal ISymbol Symbol => _symbol;
    internal TypeDeclarationSyntax TypeDeclarationSyntax => _typeDeclarationSyntax;

    public void AddMethod(MethodNode method)
    {
        _methods.Add(method);
    }

    internal void CompleteReferences(Dictionary<ISymbol, BaseTypeDeclarationNode> typeSymbolMap)
    {
        foreach (var @base in _bases)
        {
            if(typeSymbolMap.TryGetValue(@base, out BaseTypeDeclarationNode typeDeclarationNode))
            {
                _inherits.Add(typeDeclarationNode);
                typeDeclarationNode.AddInheritableDescendant(this);
            }
        }
    }

    private void AddInheritableDescendant(BaseTypeDeclarationNode descendant)
    {
        _inheritedBy.Add(descendant);
    }

    public void AddVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        _variables.Add(variableDeclaration);
    }

    public void GetAllProcessableNodes(HashSet<IFixableNode> result)
    {
        foreach (MethodNode method in _methods)
        {
            if(!result.Add(method))
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
}


public class InterfaceNode : BaseTypeDeclarationNode
{
    public InterfaceNode(ISymbol symbol, InterfaceDeclarationSyntax node, DocumentNode parent)
        : base(symbol, node, parent)
    {
    }
}

public class StructNode : BaseTypeDeclarationNode
{
    public StructNode(ISymbol symbol, StructDeclarationSyntax node, DocumentNode parent)
        : base(symbol, node, parent)
    {
    }

}

public class RecordNode : BaseTypeDeclarationNode
{
    public RecordNode(ISymbol symbol, RecordDeclarationSyntax node, DocumentNode parent)
        : base(symbol, node, parent)
    {
    }

}

public class ClassNode : BaseTypeDeclarationNode
{
    public ClassNode(ISymbol @class, ClassDeclarationSyntax classDeclaration, DocumentNode parent)
        : base(@class, classDeclaration, parent)
    {
    }
}