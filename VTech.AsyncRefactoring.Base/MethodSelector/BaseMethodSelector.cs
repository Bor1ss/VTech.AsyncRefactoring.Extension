using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public abstract class BaseMethodSelector : IMethodSelector
{
    public abstract IEnumerable<MethodNode> Select(SolutionNode solution);

    protected void SelectMethod(HashSet<MethodNode> methodNodes, MethodNode currentMethod)
    {
        if(!methodNodes.Add(currentMethod))
        {
            return;
        }

        foreach(var method in currentMethod.GetRelatedMethods())
        {
            SelectMethod(methodNodes, method);
        }
    }
}
