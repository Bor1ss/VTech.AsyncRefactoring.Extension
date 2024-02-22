using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
using VTech.AsyncRefactoring.Base.MethodSelector;

namespace VTech.AsyncRefactoring.Base;

public sealed class AsyncronizationProcessor
{
    private SolutionNode _node;
    private readonly Func<Task<SolutionNode>> _solutionNodeFactory;

    static AsyncronizationProcessor()
    {
        if (MSBuildLocator.IsRegistered || !MSBuildLocator.CanRegister)
        {
            return;
        }

        MSBuildLocator.RegisterDefaults();
    }

    public AsyncronizationProcessor(string solutionPath)
    {
        _solutionNodeFactory = () => SolutionNode.CreateAsync(solutionPath);
    }

    public AsyncronizationProcessor(Solution solution)
    {
        _solutionNodeFactory = () => SolutionNode.CreateAsync(solution);
    }

    public async Task InitializeCodeMapAsync()
    {
        _node = await _solutionNodeFactory();
    }

    public List<VTech.AsyncRefactoring.Base.Changes.ProjectChanges> CollectSuggestedChanges(IMethodSelector methodSelector)
    {
        _node.DetectIssues(methodSelector);
        _node.PrepareFixes();

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

        _node.Print();


        return projectChanges;
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

        Console.WriteLine("Done");
    }
}
