using VTech.AsyncRefactoring.Base.CodeGraph.Nodes;

namespace VTech.AsyncRefactoring.Base.CodeGraph;

public sealed class GraphBuilderOptions
{
    public SymbolInfoStorage SymbolInfoStorage { get; set; }
    public Document MsDocument { get; set; }
    public SemanticModel Model { get; set; }
    public DocumentNode Document { get; set; }
    public List<SemanticModel> AllSemanticModels { get; set; }
    public Dictionary<string, HashSet<SemanticModel>> MethodSemanticModelsMap { get; set; }
}

