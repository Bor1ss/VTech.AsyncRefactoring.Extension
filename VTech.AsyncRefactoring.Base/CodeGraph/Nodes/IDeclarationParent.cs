namespace VTech.AsyncRefactoring.Base.CodeGraph.Nodes;
public interface IDeclarationParent
{
    void AddMethod(MethodNode method);
    void AddVariableDeclaration(VariableDeclarationNode variableDeclaration);
}
