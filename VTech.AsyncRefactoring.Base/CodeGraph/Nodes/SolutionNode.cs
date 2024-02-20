using VTech.AsyncRefactoring.Base.MethodSelector;
using VTech.AsyncRefactoring.Base.Rules;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class SolutionNode
{
    private readonly MSBuildWorkspace _workspace;
    private Solution _solution;
    private readonly List<ProjectNode> _projects = [];

    private SolutionNode(MSBuildWorkspace workspace, Solution solution)
    {
        _workspace = workspace;
        _solution = solution;
    }

    public IReadOnlyCollection<ProjectNode> Projects => _projects;

    private async Task InitProjectsAsync()
    {
        ProjectDependencyGraph graph = _solution.GetProjectDependencyGraph();
        List<Project> projects = graph
            .GetTopologicallySortedProjects()
            .Select(_solution.GetProject)
            .Where(x => x is not null)
            .Select(x => x!)
        .ToList();


        List<SyntaxTree> syntaxTrees = [];
        List<(Project project, List<(Document doc, SyntaxTree tree)> docs)> projDocs = [];

        foreach (var msProject in projects)
        {
            List<(Document doc, SyntaxTree tree)> docs = [];
            foreach (var doc in msProject.Documents)
            {
                var syntaxTree = await doc.GetSyntaxTreeAsync();

                if (syntaxTree is null)
                {
                    continue;
                }
                docs.Add((doc, syntaxTree));
                syntaxTrees.Add(syntaxTree);
            }
            projDocs.Add((msProject, docs));
        }

        CSharpCompilation compilation = CSharpCompilation.Create("MyCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                           MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                           MetadataReference.CreateFromFile(typeof(Task<>).Assembly.Location))
            .AddSyntaxTrees(syntaxTrees);

        foreach (var (project, docs) in projDocs)
        {
            _projects.Add(await ProjectNode.CreateAsync(this, project, compilation, docs));
        }

        List<DocumentNode> allDocuments = _projects.SelectMany(x => x.Documents).ToList();
        List<SemanticModel> allSemanticModels = allDocuments.Select(d => d.SemanticModel).ToList();

        foreach (DocumentNode document in allDocuments)
        {
            await document.InitMethodsAsync(allSemanticModels);
        }
    }

    public async static Task<SolutionNode> CreateAsync(string path)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (sender, e) => Console.WriteLine($"[failed] {e.Diagnostic}");

        var msSolution = await workspace.OpenSolutionAsync(path);

        var solution = new SolutionNode(workspace, msSolution);
        await solution.InitProjectsAsync();

        solution.CompleteReferences();

        return solution;
    }

    private void CompleteReferences()
    {
        Dictionary<ISymbol, BaseTypeDeclarationNode> typeSymbolMap = AllTypes
            .ToDictionary(x => x.Symbol, x => x, SymbolEqualityComparer.Default);

        foreach(var typeSymbol in AllTypes)
        {
            typeSymbol.CompleteReferences(typeSymbolMap);
        }

        Dictionary<ISymbol, MethodNode> symbolMethodMap = AllMethods
               .ToDictionary(x => x.Symbol, x => x, SymbolEqualityComparer.Default);

        SymbolInfoStorage.Instance.Fill(symbolMethodMap);

        foreach (var methodNode in symbolMethodMap.Values)
        {
            methodNode.CompleteReferences(symbolMethodMap);
        }
    }

    private IEnumerable<MethodNode> AllMethods => AllTypes
        .SelectMany(x => x.Methods);

    private IEnumerable<BaseTypeDeclarationNode> AllTypes => _projects
        .SelectMany(x => x.Documents)
        .SelectMany(x => x.TypeDeclarationNodes);

    public void Print()
    {
        foreach (var project in _projects)
        {
            project.Print();
        }
    }

    internal void DetectIssues(IMethodSelector methodSelector)
    {
        List<IRule> rules =
        [
            new WaitRule(),
            new GetAwaiterGetResultRule(),
            new ResultRule(),
        ];

        foreach (var method in methodSelector.Select(this))
        {
            method.DetectIssues(rules);
        }
    }

    internal void PrepareFixes()
    {
        foreach (var method in AllMethods.OrderByDescending(x => x.Depth))
        {
            method.AsynchronizeMethod();
        }

        foreach (var method in AllMethods)
        {
            method.AsynchronizeCalls();
        }
    }

    internal async Task FixAsync()
    {
        var solution = _solution;
               
        foreach (var doc in _projects.SelectMany(x => x.Documents))
        {
            await doc.SaveAsync();
        }

        var isUpdated = _workspace.TryApplyChanges(solution);

        if (isUpdated)
        {
            _solution = solution;
        };
    }
}