namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class ProjectNode
{
    private static string[] _skippableFiles = ["GlobalUsings.g.cs", ".AssemblyAttributes.cs", ".AssemblyInfo.cs"];
    private readonly SolutionNode _parent;
    private readonly Project _project;
    private readonly Compilation _compilation;

    private readonly List<DocumentNode> _documents = [];

    private ProjectNode(SolutionNode parent, Project project, Compilation compilation)
    {
        _parent = parent;
        _project = project;
        _compilation = compilation;
    }

    private async Task InitDocumentsAsync(List<(Document doc, SyntaxTree tree)> docs)
    {
        foreach (var (doc, tree) in docs)
        {
            if(_skippableFiles.Any(x => doc.FilePath.EndsWith(x)))
            {
                continue;
            }

            _documents.Add(await DocumentNode.CreateAsync(this, doc, tree));
        }
    }

    public static async Task<ProjectNode> CreateAsync(SolutionNode parent, Project msProject)
    {
        (Compilation compilation, List<(Document doc, SyntaxTree tree)> docs) = await GetCompilationAsync(msProject);
        var project = new ProjectNode(parent, msProject, compilation);
        await project.InitDocumentsAsync(docs);
        return project;
    }

    public static async Task<ProjectNode> CreateAsync(SolutionNode parent, Project msProject, Compilation compilation, List<(Document doc, SyntaxTree tree)> docs)
    {
        var project = new ProjectNode(parent, msProject, compilation);
        await project.InitDocumentsAsync(docs);
        return project;
    }

    private static async Task<(Compilation compilation, List<(Document doc, SyntaxTree tree)> docs)> GetCompilationAsync(Project msProject)
    {
        List<(Document doc, SyntaxTree tree)> docs = [];
        List<SyntaxTree> syntaxTrees = [];

        foreach (var doc in msProject.Documents)
        {
            var syntaxTree = await doc.GetSyntaxTreeAsync();

            if(syntaxTree is null)
            {
                continue;
            }

            syntaxTrees.Add(syntaxTree);
            docs.Add((doc, syntaxTree));
        }

        CSharpCompilation compilation = CSharpCompilation.Create("MyCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                           MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                           MetadataReference.CreateFromFile(typeof(Task<>).Assembly.Location))
            .AddSyntaxTrees(syntaxTrees);

        return (compilation, docs);
    }

    internal string Id => _project.Name;
    internal IReadOnlyList<DocumentNode> Documents => _documents;
    internal Compilation Compilation => _compilation;

    public void Print()
    {
        Console.WriteLine($"- {_project.Name}");

        foreach (var doc in _documents)
        {
            doc.Print();
        }
    }
}