using Microsoft.CodeAnalysis.Text;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class DocumentNode
{
    private readonly ProjectNode _parent;
    private readonly Document _document;
    private readonly SyntaxTree _tree;
    private readonly SyntaxNode _root;
    private readonly List<BaseTypeDeclarationNode> _typeDeclarations = [];
    private readonly SemanticModel _semanticModel;

    private DocumentNode(ProjectNode parent, Document document, SemanticModel model, SyntaxTree syntaxTree, SyntaxNode syntaxRoot)
    {
        _parent = parent;
        _document = document;
        _semanticModel = model;
        _tree = syntaxTree;
        _root = syntaxRoot;
    }

    public async Task InitMethodsAsync(List<SemanticModel> allSemanticModels, SymbolInfoStorage symbolInfoStorage)
    {
        await Task.Yield();

        GraphBuilderOptions options = new()  
        { 
            MsDocument = _document, 
            Model = _semanticModel, 
            Document = this, 
            AllSemanticModels = allSemanticModels,
            SymbolInfoStorage = symbolInfoStorage
        };

        GraphBuilder graphBuilder = new(options);

        graphBuilder.Visit(_root);
    }

    public static async Task<DocumentNode> CreateAsync(ProjectNode parent, Document msDocument, SyntaxTree tree)
    {
        SyntaxNode syntaxRoot = await tree.GetRootAsync();
        SemanticModel semanticModel = parent.Compilation.GetSemanticModel(tree);
        DocumentNode document = new(parent, msDocument, semanticModel, tree, syntaxRoot);
        
        return document;
    }

    public string Id => _document.Name;
    public Document Document => _document;
    public IReadOnlyList<BaseTypeDeclarationNode> TypeDeclarationNodes => _typeDeclarations;
    internal ProjectNode Parent => _parent;
    internal SemanticModel SemanticModel => _semanticModel;
    internal SyntaxTree Tree => _tree;
    internal SyntaxNode Root => _root;
    internal bool HasChangesPrepared => _nodeReplacements.Count > 0
        || _tokenReplacements.Count > 0
        || _triviaReplacements.Count > 0;

    public void AddTypeDeclaration(BaseTypeDeclarationNode declaration)
    {
        _typeDeclarations.Add(declaration);
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

    public List<TextChange> GetDiffs()
    {
        if (!HasChangesPrepared)
        {
            return [];
        }

        SyntaxNode changedRoot = _root.ReplaceSyntax(
            _nodeReplacements.Keys, (a, _) => _nodeReplacements[a],
            _tokenReplacements.Keys, (a, _) => _tokenReplacements[a],
            _triviaReplacements.Keys, (a, _) => _triviaReplacements[a]);

        SyntaxTree changedTree = changedRoot.SyntaxTree;

        List<TextChange> changes = changedTree
            .GetChanges(_tree)
            .ToList();

        SourceText originalSourceText = Tree.GetText();
        TextLineCollection originalTextLineCollection = originalSourceText.Lines;
        string[] originalLines = originalTextLineCollection.Select(x => x.ToString()).ToArray();
        string[] changedLines = changedTree.GetText().Lines.Select(x => x.ToString()).ToArray();

        List<TextChange> finalChanges = [];

        foreach (var change in changes)
        {
            FileLinePositionSpan oldLinePositionSpan = Tree.GetLineSpan(change.Span);
            TextLine[] textLines = originalTextLineCollection
                .Skip(oldLinePositionSpan.StartLinePosition.Line)
                .Take(oldLinePositionSpan.EndLinePosition.Line - oldLinePositionSpan.StartLinePosition.Line + 1)
                .ToArray(); // need multiple lines
            TextSpan oldFirstLineSpan = textLines[0].Span;
            TextSpan allLinesSpan = new(oldFirstLineSpan.Start, textLines.Sum(x => x.Span.Length));

            int changeStartAtLine = change.Span.Start - oldFirstLineSpan.Start;
            int changeLength = change.Span.Length;

            string originalLine = originalSourceText.GetSubText(allLinesSpan).ToString();
            string changedLine = string.Empty;
            
            if(changeStartAtLine > 0)
            {
                changedLine += originalLine.Substring(0, changeStartAtLine);
            }

            changedLine += change.NewText + originalLine.Substring(changeStartAtLine + changeLength);

            finalChanges.Add(new TextChange(allLinesSpan, changedLine));
        }

        return finalChanges;
    }

    public async Task SaveAsync()
    {
        if(!HasChangesPrepared)
        {
            return;
        }

        SyntaxNode changedRoot = _root.ReplaceSyntax(
            _nodeReplacements.Keys, (a, _) => _nodeReplacements[a],
            _tokenReplacements.Keys, (a, _) => _tokenReplacements[a],
            _triviaReplacements.Keys, (a, _) => _triviaReplacements[a]);

        var text = changedRoot.GetText();

        await Task.Run(() => System.IO.File.WriteAllText(_document.FilePath, text.ToString()));
    }

    internal void ApplyChanges(List<TextChange> textChanges)
    {
        var text = _root.GetText().WithChanges(textChanges);

        System.IO.File.WriteAllText(_document.FilePath, text.ToString());
    }
}