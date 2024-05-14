using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis.Text;

namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

public class SolutionNode
{
    private static readonly string[] _skippableFiles = ["GlobalUsings.g.cs", ".AssemblyAttributes.cs", ".AssemblyInfo.cs"];

    private readonly Solution _solution;
    private readonly List<ProjectNode> _projects = [];

    private SolutionNode(Solution solution)
    {
        _solution = solution;
    }

    public IReadOnlyCollection<ProjectNode> Projects => _projects;

    private async Task InitProjectsAsync(SymbolInfoStorage symbolInfoStorage)
    {
        ProjectDependencyGraph graph = _solution.GetProjectDependencyGraph();
        List<Project> projects = graph
            .GetTopologicallySortedProjects()
            .Select(_solution.GetProject)
            .Where(x => x is not null)
            .Select(x => x!)
        .ToList();

        List<SyntaxTree> syntaxTrees = [];
        List<(Project project, List<(Document doc, SyntaxTree tree, bool CustomUsingsAdded)> docs)> projDocs = [];

        var options = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithFeatures([
                new KeyValuePair<string, string>("flow-analysis", ""),
                new KeyValuePair<string, string>("InterceptorsPreviewNamespaces", "System;System.Collections.Generic;System.IO;System.Linq;System.Net.Http;System.Threading;System.Threading.Tasks"),
            ]);

        List<string> dllPaths = [];

        foreach (Project msProject in projects.Select(x => x.WithParseOptions(options)))
        {
            IEnumerable<string> metaFiles = msProject.MetadataReferences.Select(x => x.Display).Where(File.Exists);
            dllPaths.AddRange(metaFiles);

            bool hasImplicitUsings = false;
            if(File.Exists(msProject.FilePath))
            {
                string csprojContent = File.ReadAllText(msProject.FilePath);
                hasImplicitUsings = Regex.IsMatch(csprojContent, "<ImplicitUsings>\\s*enable\\s*</ImplicitUsings>", RegexOptions.IgnoreCase & RegexOptions.Multiline)
                    || Regex.IsMatch(csprojContent, "<ImplicitUsings>\\s*enabled\\s*</ImplicitUsings>", RegexOptions.IgnoreCase & RegexOptions.Multiline)
                    || Regex.IsMatch(csprojContent, "<ImplicitUsings>\\s*true\\s*</ImplicitUsings>", RegexOptions.IgnoreCase & RegexOptions.Multiline);
            }

            List<(Document doc, SyntaxTree tree, bool CustomUsingsAdded)> docs = [];
            foreach (Document doc in msProject.Documents)
            {
                if (_skippableFiles.Any(x => doc.FilePath.EndsWith(x)))
                {
                    continue;
                }

                SyntaxTree syntaxTree = await doc.GetSyntaxTreeAsync();

                if (syntaxTree is null)
                {
                    continue;
                }

                if(hasImplicitUsings)
                {
                    var newText = syntaxTree.GetRoot().GetText().WithChanges(new TextChange(new TextSpan(0, 0), $"using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Net.Http;using System.Threading;using System.Threading.Tasks{Environment.NewLine}"));
                    syntaxTree = syntaxTree.WithChangedText(newText);
                }

                docs.Add((doc, syntaxTree, hasImplicitUsings));
                syntaxTrees.Add(syntaxTree);
            }
            projDocs.Add((msProject, docs));
        }

        List<PortableExecutableReference> references = dllPaths
            .Distinct()
            .Select(x => MetadataReference.CreateFromFile(x))
            .ToList();

        CSharpCompilation compilation = CSharpCompilation.Create("MyCompilation")
            .AddReferences(references)
            .AddSyntaxTrees(syntaxTrees);

        foreach (var (project, docs) in projDocs)
        {
            _projects.Add(await ProjectNode.CreateAsync(this, project, compilation, docs));
        }

        List<DocumentNode> allDocuments = _projects.SelectMany(x => x.Documents).ToList();
        List<SemanticModel> allSemanticModels = allDocuments.Select(d => d.SemanticModel).ToList();

        Dictionary<string, HashSet<SemanticModel>> methodSemanticModelsMap = [];
        foreach (DocumentNode document in allDocuments)
        {
            foreach(MethodDeclarationSyntax methodDeclaration in document.Tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                string methodName = methodDeclaration.Identifier.Text;
                if (string.IsNullOrWhiteSpace(methodName) || string.Equals(methodName, "ToString"))
                {
                    continue;
                }

                if(!methodSemanticModelsMap.ContainsKey(methodName))
                {
                    methodSemanticModelsMap[methodName] = [];
                }

                methodSemanticModelsMap[methodName].Add(document.SemanticModel);
            }

            foreach(LocalFunctionStatementSyntax localFunctionStatement in document.Tree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                string methodName = localFunctionStatement.Identifier.Text;
                if (string.IsNullOrWhiteSpace(methodName) || string.Equals(methodName, "ToString"))
                {
                    continue;
                }

                if(!methodSemanticModelsMap.ContainsKey(methodName))
                {
                    methodSemanticModelsMap[methodName] = [];
                }

                methodSemanticModelsMap[methodName].Add(document.SemanticModel);
            }
        }

        foreach (DocumentNode document in allDocuments)
        {
            await document.InitMethodsAsync(allSemanticModels, symbolInfoStorage, methodSemanticModelsMap);
        }
    }

    public async static Task<SolutionNode> CreateAsync(string path, SymbolInfoStorage symbolInfoStorage)
    {
        MSBuildWorkspace workspace = MSBuildWorkspace.Create();
        workspace.WorkspaceFailed += (sender, e) => Console.WriteLine($"[failed] {e.Diagnostic}");

        Solution msSolution = await workspace.OpenSolutionAsync(path);

        return await CreateAsync(msSolution, symbolInfoStorage);
    }

    public async static Task<SolutionNode> CreateAsync(Solution msSolution, SymbolInfoStorage symbolInfoStorage)
    {
        SolutionNode solution = new(msSolution);
        await solution.InitProjectsAsync(symbolInfoStorage);

        solution.CompleteReferences(symbolInfoStorage);

        return solution;
    }

    private void CompleteReferences(SymbolInfoStorage symbolInfoStorage)
    {
        List<BaseTypeDeclarationNode> allTypes = _projects
            .SelectMany(x => x.Documents)
            .SelectMany(x => x.TypeDeclarationNodes)
            .ToList();

        Dictionary<ISymbol, BaseTypeDeclarationNode> typeSymbolMap = allTypes
            .GroupBy(x => x.Symbol, SymbolEqualityComparer.Default)
            .ToDictionary(x => x.Key, x => x.First(), SymbolEqualityComparer.Default);

        foreach (var typeSymbol in allTypes)
        {
            typeSymbol.CompleteReferences(typeSymbolMap);
        }

        Dictionary<ISymbol, MethodNode> symbolMethodMap = allTypes
            .SelectMany(x => x.Methods)
            .ToDictionary(x => x.Symbol, x => x, SymbolEqualityComparer.Default);

        symbolInfoStorage.Fill(symbolMethodMap);

        foreach (var methodNode in symbolMethodMap.Values)
        {
            methodNode.CompleteReferences(symbolMethodMap);
        }
    }
}