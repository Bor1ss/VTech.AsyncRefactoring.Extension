using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.CodeGraph;
public class SymbolInfoStorage
{
    private readonly List<Item> _items = [];

    [System.Runtime.CompilerServices.IndexerName("Method")]
    public MethodNode this[ISymbol symbol] => _items.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Symbol, symbol))?.Method;

    [System.Runtime.CompilerServices.IndexerName("Method")]
    public MethodNode this[InvocationExpressionSyntax invocation] => _items.FirstOrDefault(x => x.InvocationExpressions.Contains(invocation))?.Method;

    [System.Runtime.CompilerServices.IndexerName("Method")]
    public MethodNode this[IdentifierNameSyntax identifierName] => _items.FirstOrDefault(x => x.IdentifierNames.Contains(identifierName))?.Method;

    internal void Fill(Dictionary<ISymbol, MethodNode> symbolMethodMap)
    {
        foreach(var symbol in symbolMethodMap.Keys)
        {
            Item item = _items.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Symbol, symbol));
            if (item is null)
            {
                continue;
            }

            item.Method = symbolMethodMap[symbol];
        }
    }

    internal void Set(InvocationExpressionSyntax invocation, ISymbol invocationSymbol)
    {
        Item item = _items.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Symbol, invocationSymbol));

        if (item is null)
        {
            item = new(invocationSymbol);

            _items.Add(item);
        }

        item.InvocationExpressions.Add(invocation);
    }

    internal void Set(IdentifierNameSyntax identifier, ISymbol invocationSymbol)
    {
        Item item = _items.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.Symbol, invocationSymbol));

        if (item is null)
        {
            item = new(invocationSymbol);

            _items.Add(item);
        }

        item.IdentifierNames.Add(identifier);
    }


    private class Item
    {
        public Item(ISymbol symbol) 
        {
            Symbol = symbol;
        }

        public HashSet<InvocationExpressionSyntax> InvocationExpressions { get; set; } = [];
        public HashSet<IdentifierNameSyntax> IdentifierNames { get; set; } = [];
        public ISymbol Symbol { get; private set; }
        public MethodNode Method { get; set; }
    }
}
