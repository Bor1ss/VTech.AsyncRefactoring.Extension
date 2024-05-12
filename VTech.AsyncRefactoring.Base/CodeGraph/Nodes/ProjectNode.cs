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

    private async Task InitDocumentsAsync(List<(Document doc, SyntaxTree tree, bool CustomUsingsAdded)> docs)
    {
        foreach (var (doc, tree, customUsingAdded) in docs)
        {
            if(_skippableFiles.Any(x => doc.FilePath.EndsWith(x)))
            {
                continue;
            }

            _documents.Add(await DocumentNode.CreateAsync(this, doc, tree, customUsingAdded));
        }
    }

    public static async Task<ProjectNode> CreateAsync(SolutionNode parent, Project msProject, Compilation compilation, List<(Document doc, SyntaxTree tree, bool CustomUsingsAdded)> docs)
    {
        var project = new ProjectNode(parent, msProject, compilation);
        await project.InitDocumentsAsync(docs);
        return project;
    }

    public string Id => _project.Name;
    public IReadOnlyList<DocumentNode> Documents => _documents;
    internal Compilation Compilation => _compilation;
}