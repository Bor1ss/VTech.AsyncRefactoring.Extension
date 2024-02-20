using Microsoft.Build.Locator;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base;

public sealed class AsyncronizationProcessor
{
    private readonly string _solutionPath;
    private SolutionNode _node;

    static AsyncronizationProcessor()
    {
        MSBuildLocator.RegisterDefaults();
    }

    public AsyncronizationProcessor(string solutionPath)
    {
        _solutionPath = solutionPath;
    }

    public async Task InitializeCodeMapAsync()
    {
        _node = await SolutionNode.CreateAsync(_solutionPath);
    }

    public object CollectSuggestedChanges(object entryPoint)
    {
        _node.DetectIssues();

        _node.Print();

        return null;
    }

    public async Task ApplyChangesAsync(object chamges)
    {
        await _node.FixAsync();
        Console.WriteLine("Done");
    }
}
