using System.Collections;
using System;
using System.Collections.Generic;

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

        Dictionary<TextSpan, List<TextChange>> changesForSpan = [];
        List<KeyValuePair<TextSpan, TextChange>> spanChange = [];

        List<TextChange> finalChanges = [];

        //assosiate each change with line
        for (int i = 0; i < changes.Count; i++)
        {
            TextChange change = changes[i];
            FileLinePositionSpan oldLinePositionSpan = Tree.GetLineSpan(change.Span);
            TextLine[] textLines = originalTextLineCollection
                .Skip(oldLinePositionSpan.StartLinePosition.Line)
                .Take(oldLinePositionSpan.EndLinePosition.Line - oldLinePositionSpan.StartLinePosition.Line + 1)
                .ToArray();
            TextSpan oldFirstLineSpan = textLines[0].Span;
            TextSpan allLinesSpan = new(oldFirstLineSpan.Start, textLines.Sum(x => x.SpanIncludingLineBreak.Length));

            spanChange.Add(new (allLinesSpan, change));

            //if(i > 0)
            //{
            //    TextChange prevChange = finalChanges.Last();
            //    if(prevChange.Span.End == (allLinesSpan.Start + 1))
            //    {
            //        finalChanges.Remove(prevChange);

            //        allLinesSpan = new TextSpan(prevChange.Span.Start, prevChange.Span.Length + allLinesSpan.Length);
            //        changedLine = prevChange.NewText + changedLine;
            //    }
            //}

            //finalChanges.Add(new TextChange(allLinesSpan, changedLine));
        }

        TextSpan currentSpan = spanChange[0].Key;
        List<TextChange> changesForCurrentSpan = [spanChange[0].Value];

        //group line changes
        for(int i = 1; i < spanChange.Count; i++)
        {
            bool isIntersected = currentSpan.IntersectsWith(spanChange[i].Key);
            if(!isIntersected)
            {
                changesForSpan.Add(currentSpan, changesForCurrentSpan);
                currentSpan = spanChange[i].Key;
                changesForCurrentSpan = [spanChange[i].Value];

                continue;
            }

            int comboStart = Math.Min(currentSpan.Start, spanChange[i].Key.Start);
            int comboEnd = Math.Max(currentSpan.End, spanChange[i].Key.End);

            currentSpan = currentSpan.Intersection(spanChange[i].Key).Value;

            changesForCurrentSpan.Add(spanChange[i].Value);
        }

        changesForSpan.Add(currentSpan, changesForCurrentSpan);

        //apply changes to line
        foreach (TextSpan textSpan in changesForSpan.Keys)
        {
            string originalLine = originalSourceText.GetSubText(textSpan).ToString();

            for (int i = changesForSpan[textSpan].Count - 1; i >= 0; i--)
            {
                string changedLine = string.Empty;

                TextChange change = changesForSpan[textSpan][i];

                int changeStartAtLine = change.Span.Start - textSpan.Start;
                int changeLength = change.Span.Length;

                if (changeStartAtLine > 0)
                {
                    changedLine += originalLine.Substring(0, changeStartAtLine);
                }

                originalLine = changedLine + change.NewText + originalLine.Substring(changeStartAtLine + changeLength);
            }

            finalChanges.Add(new TextChange(textSpan, originalLine));
        }

        return finalChanges;
    }

    public async Task SaveAsync()
    {
        if (!HasChangesPrepared)
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