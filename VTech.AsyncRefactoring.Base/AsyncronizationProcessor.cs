using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;

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
    {
        _solutionNodeFactory = () => SolutionNode.CreateAsync(solutionPath, _symbolInfoStorage);
    }

    public AsyncronizationProcessor(Solution solution)
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

        foreach(ProjectNode project in _node.Projects)
        {
            VTech.AsyncRefactoring.Base.Changes.ProjectChanges curProjectChanges = new()
            {
                Id = project.Id
            };

            foreach (DocumentNode document in project.Documents)
            {
                if(!document.HasChangesPrepared)
                { 
                    continue; 
                }

                VTech.AsyncRefactoring.Base.Changes.DocumentChanges curDocumentChanges = new()
                {
                    Id = document.Id,
                    TextChanges = document.GetDiffs()
                };

                curProjectChanges.Documents.Add(curDocumentChanges);
            }

            if(curProjectChanges.Documents.Count > 0)
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

    public async Task ApplyChangesAsync(List<VTech.AsyncRefactoring.Base.Changes.ProjectChanges> changes)
    {
        foreach(VTech.AsyncRefactoring.Base.Changes.ProjectChanges projectChanges in changes)
        {
            ProjectNode project = _node.Projects.First(x => x.Id == projectChanges.Id);
            foreach(VTech.AsyncRefactoring.Base.Changes.DocumentChanges documentChanges in projectChanges.Documents)
            {
                DocumentNode document = project.Documents.First(x => x.Id == documentChanges.Id);
                document.ApplyChanges(documentChanges.TextChanges);
            }
        }
    }
}
