using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class ProjectRelatedMethodSelector : BaseMethodSelector
{
    private readonly string _project;

    public ProjectRelatedMethodSelector(string project)
    {
        _project = project;
    }

    public override IEnumerable<MethodNode> Select(SolutionNode solution)
    {
        HashSet<MethodNode> result = [];

        var projectMethods = solution.Projects
            .First(x => x.Id == _project)
            .Documents
            .SelectMany(x => x.TypeDeclarationNodes)
            .SelectMany(x => x.Methods);

        foreach (var method in projectMethods)
        {
            SelectMethod(result, method);
        }

        return result;
    }
}
