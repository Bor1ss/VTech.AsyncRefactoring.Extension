using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public interface IMethodSelector
{
    IEnumerable<MethodNode> Select(SolutionNode solution);
}
