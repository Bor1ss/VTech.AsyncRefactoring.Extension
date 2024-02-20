namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public abstract class BaseTypeDeclarationNode
{
    protected readonly ISymbol _symbol;
    protected readonly TypeDeclarationSyntax _typeDeclarationSyntax;
    protected readonly DocumentNode _parent;
    protected readonly List<MethodNode> _methods = [];
    protected readonly List<BaseTypeDeclarationNode> _inherits = [];
    protected readonly List<BaseTypeDeclarationNode> _inheritedBy = [];
    protected readonly List<INamedTypeSymbol> _bases = [];

    public DocumentNode Parent => _parent;

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

    internal IReadOnlyList<MethodNode> Methods => _methods;
    internal IReadOnlyList<BaseTypeDeclarationNode> Bases => _inherits;

    internal ISymbol Symbol => _symbol;
    internal TypeDeclarationSyntax TypeDeclarationSyntax => _typeDeclarationSyntax;

    public void AddMethod(MethodNode method)
    {
        _methods.Add(method);
    }

    internal void Print()
    {
        Console.WriteLine($"|-|-|-> {_symbol.Name}");
        foreach (var method in _methods)
        {
            method.Print("|-|-|-|->");
        }
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

[Flags]
public enum ClassAttribute
{
    None = 0,
    Abstract = 1,
    Sealed = 2,
    Partial = 4
}

public class ClassNode : BaseTypeDeclarationNode
{
    private ClassAttribute _attributes = ClassAttribute.None;

    public ClassNode(ISymbol @class, ClassDeclarationSyntax classDeclaration, DocumentNode parent)
        : base(@class, classDeclaration, parent)
    {
    }
}