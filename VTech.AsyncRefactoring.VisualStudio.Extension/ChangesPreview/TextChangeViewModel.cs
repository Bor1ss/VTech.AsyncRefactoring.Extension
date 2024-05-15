using VTech.AsyncRefactoring.Base.Changes;
using VTech.AsyncRefactoring.VisualStudio.Extension.Shared;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;

public class TextChangeViewModel : NotifiableBase
{
    private readonly TextChange _textChange;
    public TextChangeViewModel(TextChange textChange)
    {
        _textChange = textChange;
        _isSelected = true;
    }

    public TextChange TextChange => _textChange;

    public string OldText
    {
        get => _textChange.OldText;
        set
        {
            if (_textChange.OldText != value)
            {
                _textChange.OldText = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string NewText
    {
        get => _textChange.NewText;
        set
        {
            if (_textChange.NewText != value)
            {
                _textChange.NewText = value;
                OnPropertyChanged();
            }
        }
    }
}
