namespace VTech.AsyncRefactoring.Base.Changes;

public sealed class DocumentChanges
{
    public string Id { get; set; }
    public List<TextChange> TextChanges { get; set; }
}
