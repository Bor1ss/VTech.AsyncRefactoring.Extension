using System;

using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
using VTech.AsyncRefactoring.Base.Utils;

namespace VTech.AsyncRefactoring.Console.Utils;
internal class CodeGraphVisualizer : ICodeGraphVisualizer
{
    public void Visualize(SolutionNode solution)
    {
        foreach (var project in solution.Projects)
        {
            System.Console.WriteLine(project.Id);
            foreach (var document in project.Documents)
            {
                foreach (var typeDeclaration in document.TypeDeclarationNodes)
                {
                    System.Console.WriteLine($"|=> {typeDeclaration.Id}");
                    foreach (var method in typeDeclaration.Methods)
                    {
                        PrintMethod(method, 1);
                    }
                }
            }
        }
    }

    private void PrintMethod(MethodNode method, int depth)
    {
        if(method is null)
        {
            return;
        }

        var prevColor = System.Console.ForegroundColor;

        if (method.NeedsAsynchronization)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
        }

        string printPref = "|=";

        for(int i = 0; i < depth; i++)
        {
            printPref += "=";
        }

        printPref += ">";

        System.Console.WriteLine($"{printPref} {method.Name}");

        System.Console.ForegroundColor = prevColor;

        foreach (var internalMethod in method.InternalMethods)
        {
            PrintMethod(internalMethod, depth + 1);
        }
    }
}
