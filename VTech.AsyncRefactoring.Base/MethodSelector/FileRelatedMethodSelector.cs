using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class FileRelatedMethodSelector : BaseMethodSelector
{
    private readonly string _project;
    private readonly string _file;

    public FileRelatedMethodSelector(string project, string file)
    {
        _project = project;
        _file = file;
    }

    public override IEnumerable<MethodNode> Select(SolutionNode solution)
    {
        var project = solution.Projects.First(x => x.Id == _project);
        var doc = project.Documents.First(x => x.Id == _file);

        HashSet<MethodNode> result = [];

        var fileMethods = doc.TypeDeclarationNodes.SelectMany(x => x.Methods);
        foreach (var method in fileMethods)
        {
            SelectMethod(result, method);
        }

        return result;
    }
}
