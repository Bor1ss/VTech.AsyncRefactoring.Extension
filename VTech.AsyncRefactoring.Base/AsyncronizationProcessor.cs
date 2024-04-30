using Microsoft.Build.Locator;

using VTech.AsyncRefactoring.Base.CodeGraph;
using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
using VTech.AsyncRefactoring.Base.MethodSelector;
using VTech.AsyncRefactoring.Base.Rules;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Base;

public sealed class AsyncronizationProcessor
{
    private readonly SymbolInfoStorage _symbolInfoStorage;
    private readonly Func<Task<SolutionNode>> _solutionNodeFactory;
    private readonly IReadOnlyList<IRule> _rulesSet;
    private SolutionNode _node;

    static AsyncronizationProcessor()
    {
        if (MSBuildLocator.IsRegistered || !MSBuildLocator.CanRegister)
        {
            return;
        }

        MSBuildLocator.RegisterDefaults();
    }

    private AsyncronizationProcessor()
    {
        _symbolInfoStorage = new SymbolInfoStorage();
        _rulesSet = [
            new WaitRule(_symbolInfoStorage),
            new GetAwaiterGetResultRule(_symbolInfoStorage),
            new ResultRule(_symbolInfoStorage),
        ];
    }

    public AsyncronizationProcessor(string solutionPath)
        : this()
    {
        _solutionNodeFactory = () => SolutionNode.CreateAsync(solutionPath, _symbolInfoStorage);
    }

    public AsyncronizationProcessor(Solution solution)
        : this()
    {
        _solutionNodeFactory = () => SolutionNode.CreateAsync(solution, _symbolInfoStorage);
    }

    public async Task InitializeCodeMapAsync()
    {
        _node = await _solutionNodeFactory();
    }

    private void DetectIssues(List<MethodNode> processableMethods)
    {
        foreach (var method in processableMethods)
        {
            method.DetectIssues(_rulesSet);
        }
    }

    private void PrepareFixes(List<MethodNode> processableMethods)
    {
        foreach (var method in processableMethods.OrderByDescending(x => x.Depth))
        {
            method.PrepareFixes(_symbolInfoStorage);
        }
    }

    public List<VTech.AsyncRefactoring.Base.Changes.ProjectChanges> CollectSuggestedChanges(IMethodSelector methodSelector)
    {
        List<MethodNode> processableMethods = methodSelector.Select(_node).ToList();

        DetectIssues(processableMethods);
        PrepareFixes(processableMethods);

        List<VTech.AsyncRefactoring.Base.Changes.ProjectChanges> projectChanges = new(_node.Projects.Count);

        foreach (ProjectNode project in _node.Projects)
        {
            VTech.AsyncRefactoring.Base.Changes.ProjectChanges curProjectChanges = new()
            {
                Id = project.Id
            };

            foreach (DocumentNode document in project.Documents)
            {
                if (!document.HasChangesPrepared)
                {
                    continue;
                }

                VTech.AsyncRefactoring.Base.Changes.DocumentChanges curDocumentChanges = new()
                {
                    Id = document.Id,
                    TextChanges = []
                };

                foreach (var change in document.GetDiffs())
                {
                    Changes.TextChange textChange = new()
                    {
                        NewText = change.NewText,
                        OldSpanStart = change.Span.Start,
                        OldSpanLength = change.Span.Length,
                        OldText = document.Root.GetText().GetSubText(change.Span).ToString()
                    };

                    curDocumentChanges.TextChanges.Add(textChange);
                }

                curProjectChanges.Documents.Add(curDocumentChanges);
            }

            if (curProjectChanges.Documents.Count > 0)
            {
                projectChanges.Add(curProjectChanges);
            }
        }

        return projectChanges;
    }

    public void VisualizeGraph(ICodeGraphVisualizer codeGraphVisualizer)
    {
        codeGraphVisualizer.Visualize(_node);
    }

    public async Task ApplyChangesAsync(List<Changes.ProjectChanges> changes)
    {
        foreach (Changes.ProjectChanges projectChanges in changes)
        {
            ProjectNode project = _node.Projects.First(x => x.Id == projectChanges.Id);
            foreach (Changes.DocumentChanges documentChanges in projectChanges.Documents)
            {
                DocumentNode document = project.Documents.First(x => x.Id == documentChanges.Id);

                List<Microsoft.CodeAnalysis.Text.TextChange> textChanges = [];

                foreach (var textChange in documentChanges.TextChanges)
                {
                    Microsoft.CodeAnalysis.Text.TextSpan oldSpan = new(textChange.OldSpanStart, textChange.OldSpanLength);
                    Microsoft.CodeAnalysis.Text.TextChange curChange = new(oldSpan, textChange.NewText);
                    textChanges.Add(curChange);
                }

                document.ApplyChanges(textChanges);
            }
        }
    }
}
