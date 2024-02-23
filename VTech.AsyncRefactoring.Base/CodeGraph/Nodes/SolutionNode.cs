namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class SolutionNode
{
    private readonly Solution _solution;
    private readonly List<ProjectNode> _projects = [];

    private SolutionNode(Solution solution)
    {
        _solution = solution;
    }

    public IReadOnlyCollection<ProjectNode> Projects => _projects;

    private async Task InitProjectsAsync(SymbolInfoStorage symbolInfoStorage)
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

        foreach (Project msProject in projects)
        {
            List<(Document doc, SyntaxTree tree)> docs = [];
            foreach (Document doc in msProject.Documents)
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
            await document.InitMethodsAsync(allSemanticModels, symbolInfoStorage);
        }
    }

    public async static Task<SolutionNode> CreateAsync(string path, SymbolInfoStorage symbolInfoStorage)
    {
        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (sender, e) => Console.WriteLine($"[failed] {e.Diagnostic}");

        Solution msSolution = await workspace.OpenSolutionAsync(path);

        return await CreateAsync(msSolution, symbolInfoStorage);
    }

    public async static Task<SolutionNode> CreateAsync(Solution msSolution, SymbolInfoStorage symbolInfoStorage)
    {
        SolutionNode solution = new(msSolution);
        await solution.InitProjectsAsync(symbolInfoStorage);

        solution.CompleteReferences(symbolInfoStorage);

        return solution;
    }

    private void CompleteReferences(SymbolInfoStorage symbolInfoStorage)
    {
        List<BaseTypeDeclarationNode> allTypes = _projects
            .SelectMany(x => x.Documents)
            .SelectMany(x => x.TypeDeclarationNodes)
            .ToList();

        Dictionary<ISymbol, BaseTypeDeclarationNode> typeSymbolMap = allTypes
            .ToDictionary(x => x.Symbol, x => x, SymbolEqualityComparer.Default);

        foreach (var typeSymbol in allTypes)
        {
            typeSymbol.CompleteReferences(typeSymbolMap);
        }

        Dictionary<ISymbol, MethodNode> symbolMethodMap = allTypes
            .SelectMany(x => x.Methods)
            .ToDictionary(x => x.Symbol, x => x, SymbolEqualityComparer.Default);

        symbolInfoStorage.Fill(symbolMethodMap);

        foreach (var methodNode in symbolMethodMap.Values)
        {
            methodNode.CompleteReferences(symbolMethodMap);
        }
    }
}