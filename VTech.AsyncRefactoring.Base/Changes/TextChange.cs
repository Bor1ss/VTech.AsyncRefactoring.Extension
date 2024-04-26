namespace VTech.AsyncRefactoring.Base.Changes;

public sealed class TextChange
{
    public int OldSpanStart { get; set; }
    public int OldSpanLength { get; set; }

    public string OldText { get; set; }
    public string NewText { get; set; }
}