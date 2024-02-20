using Microsoft.CodeAnalysis.Text;

namespace VTech.AsyncRefactoring.Base.Changes;
public class ProjectChanges
{
    public string Id { get; set; }
    public List<DocumentChanges> Documents { get; } = [];
}

public class DocumentChanges
{
    public string Id { get; set; }
    public List<TextChange> TextChanges { get; set; }
}