using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.MethodSelector;
public class CoursorRelatedMethodSelector : BaseMethodSelector
{
    private readonly string _project;
    private readonly string _file;
    private readonly int _line;
    private readonly int _column;

    public CoursorRelatedMethodSelector(string project, string file, int line, int column)
    {
        _project = project;
        _file = file;
        _line = line;
        _column = column;
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
