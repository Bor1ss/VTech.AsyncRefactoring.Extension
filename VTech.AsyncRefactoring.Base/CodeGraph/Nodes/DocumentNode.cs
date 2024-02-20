namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class DocumentNode
{
    private readonly ProjectNode _parent;
    private readonly Document _document;
    private readonly SyntaxTree _tree;
    private SyntaxNode _root;
    private readonly List<BaseTypeDeclarationNode> _typeDeclarations = [];
    private readonly SemanticModel _semanticModel;

    private DocumentNode(ProjectNode parent, Document document, SemanticModel? model, SyntaxTree syntaxTree, SyntaxNode syntaxRoot)
    {
        _parent = parent;
        _document = document;
        _semanticModel = model!;
        _tree = syntaxTree;
        _root = syntaxRoot;
    }

    public async Task InitMethodsAsync(List<SemanticModel> allSemanticModels)
    {
        await Task.Yield();

        var options = new GraphBuilderOptions { MsDocument = _document, Model = _semanticModel, Document = this, AllSemanticModels = allSemanticModels };
        var graphBuilder = new GraphBuilder(options);

        graphBuilder.Visit(_root);
    }

    public static async Task<DocumentNode> CreateAsync(ProjectNode parent, Document msDocument, SyntaxTree tree)
    {
        var syntaxTree = tree;
        var syntaxRoot = await syntaxTree.GetRootAsync();
        SemanticModel semanticModel = parent.Compilation.GetSemanticModel(syntaxTree);
        var document = new DocumentNode(parent, msDocument, semanticModel, syntaxTree, syntaxRoot);
        //await document.InitMethodsAsync();
        return document;
    }

    internal IReadOnlyList<BaseTypeDeclarationNode> TypeDeclarationNodes => _typeDeclarations;
    internal ProjectNode Parent => _parent;
    internal SemanticModel SemanticModel => _semanticModel;
    internal SyntaxTree Tree => _tree;
    internal SyntaxNode Root => _root;

    public void AddTypeDeclaration(BaseTypeDeclarationNode declaration)
    {
        _typeDeclarations.Add(declaration);
    }

    internal void Print()
    {
        Console.WriteLine($"|-|-> {_document.Name}");
        foreach (var @class in _typeDeclarations)
        {
            @class.Print();
        }
    }

    private readonly Dictionary<SyntaxNode, SyntaxNode> _nodeReplacements = [];
    private readonly Dictionary<SyntaxToken, SyntaxToken> _tokenReplacements = [];
    private readonly Dictionary<SyntaxTrivia, SyntaxTrivia> _triviaReplacements = [];

    public void Replace(SyntaxNode old, SyntaxNode @new)
    {
        _nodeReplacements.Add(old, @new);
    }

    public void Replace(SyntaxToken old, SyntaxToken @new)
    {
        _tokenReplacements.Add(old, @new);
    }

    public void Replace(SyntaxTrivia old, SyntaxTrivia @new)
    {
        _triviaReplacements.Add(old, @new);
    }

    public async Task SaveAsync()
    {
        if(_nodeReplacements.Count == 0 && _tokenReplacements.Count == 0 && _triviaReplacements.Count == 0)
        {
            return;
        }

        _root = _root.ReplaceSyntax(
            _nodeReplacements.Keys, (a, _) => _nodeReplacements[a],
            _tokenReplacements.Keys, (a, _) => _tokenReplacements[a],
            _triviaReplacements.Keys, (a, _) => _triviaReplacements[a]);

        var text = _root.GetText();

        await Task.Run(() => System.IO.File.WriteAllText(_document.FilePath, text.ToString()));
    }
}