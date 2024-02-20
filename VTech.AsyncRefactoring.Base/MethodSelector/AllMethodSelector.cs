using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class AllMethodSelector : IMethodSelector
{
    public IEnumerable<MethodNode> Select(SolutionNode solution)
    {
        return solution.Projects
            .SelectMany(x => x.Documents)
            .SelectMany(x => x.TypeDeclarationNodes)
            .SelectMany(x => x.Methods);
    }
}
