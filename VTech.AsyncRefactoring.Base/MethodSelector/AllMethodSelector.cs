using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class AllMethodSelector : IMethodSelector
{
    public IEnumerable<IFixableNode> Select(SolutionNode solution)
    {
        HashSet<IFixableNode> result = [];

        IEnumerable<BaseTypeDeclarationNode> allTypes = solution.Projects
            .SelectMany(x => x.Documents)
            .SelectMany(x => x.TypeDeclarationNodes);

        foreach (BaseTypeDeclarationNode type in allTypes)
        {
            type.GetAllProcessableNodes(result);
        }

        return result;
    }
}
