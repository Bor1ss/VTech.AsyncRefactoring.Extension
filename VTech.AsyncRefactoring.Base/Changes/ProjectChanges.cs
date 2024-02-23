
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

public class TextChange
{
    public int OldSpanStart { get; set; }
    public int OldSpanLength { get; set; }

    public string OldText { get; set; }
    public string NewText { get; set; }
}