using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class CoursorRelatedMethodSelector : BaseMethodSelector
{
    private readonly string _project;
    private readonly string _file;
    private readonly int _line;

    public CoursorRelatedMethodSelector(string project, string file, int line)
    {
        _project = project;
        _file = file;
        _line = line;
    }

    public override IEnumerable<MethodNode> Select(SolutionNode solution)
    {
        var project = solution.Projects.First(x => x.Id == _project);
        var doc = project.Documents.First(x => x.Id == _file);

        HashSet<MethodNode> result = [];

        List<MethodNode> fileMethods = doc.TypeDeclarationNodes
            .SelectMany(x => x.Methods)
            .ToList();

        List<MethodNode> selectedMethods = fileMethods
            .Where(x => Intersects(x.Location, _line))
            .ToList();

        if(!selectedMethods.Any())
        {
            selectedMethods = fileMethods;
        }

        foreach (var method in selectedMethods)
        {
            SelectMethod(result, method);
        }

        return result;
    }

    private bool Intersects(Location location, int line)
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        if (!lineSpan.IsValid)
        {
            return false;
        }

        return lineSpan.StartLinePosition.Line <= line && lineSpan.EndLinePosition.Line >= line;
    }
}
