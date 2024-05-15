using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using VTech.AsyncRefactoring.Base.Changes;

using VTech.AsyncRefactoring.VisualStudio.Extension.Shared;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;

public class DocumentChangesViewModel : NotifiableBase
{
    private readonly DocumentChanges _documentChanges;
    public DocumentChangesViewModel(DocumentChanges docChanges)
    {
        _documentChanges = docChanges;
        _id = docChanges.Id;
        _textChanges = new(docChanges.TextChanges.Select(x => new TextChangeViewModel(x)));
        foreach (var item in _textChanges)
        {
            item.PropertyChanged += TextChangePropertyChanged;
        }
    }

    private void TextChangePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not TextChangeViewModel textChangeViewModel)
        {
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(textChangeViewModel.IsSelected)))
        {
            return;
        }

        OnPropertyChanged(nameof(HasSelectedChanges));
    }

    private string _id;
    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    private List<TextChangeViewModel> _textChanges = [];
    public List<TextChangeViewModel> TextChanges
    {
        get => _textChanges;
        set
        {
            if (_textChanges != value)
            {
                _textChanges = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasSelectedChanges => _textChanges.Any(x => x.IsSelected);
}