using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.PlatformUI;

using VTech.AsyncRefactoring.Base.Changes;

namespace VTech.AsyncRefactoring.VisualStudio.Extension;

public partial class ChangesPreviewDialog : DialogWindow
{
    public ChangesPreviewDialog(List<ProjectChanges> changes)
    {
        InitializeComponent();
        Context = new ContextVM(changes);
        DataContext = Context;
    }

    public ContextVM Context { get; private set; }
}

public class ContextVM
{
    public ContextVM(List<ProjectChanges> changes)
    {
        Changes = new(changes.Select(x => new ProjectChangesViewModel(x)));
    }

    public ObservableCollection<ProjectChangesViewModel> Changes { get; private set; } = [];

    public List<ProjectChanges> GetSelectedChanges()
    {
        if (Changes.All(x => !x.HasSelectedChanges))
        {
            return [];
        }

        List<ProjectChanges> result = [];

        foreach (var change in Changes.Where(x => x.HasSelectedChanges))
        {
            ProjectChanges projectChanges = new()
            {
                Id = change.Id
            };

            foreach(var doc in change.Documents.Where(x => x.HasSelectedChanges))
            {
                DocumentChanges documentChanges = new()
                {
                    Id = doc.Id,
                    TextChanges = []
                };

                foreach(var textChnage in doc.TextChanges.Where(x => x.IsSelected))
                {
                    documentChanges.TextChanges.Add(textChnage.TextChange);
                }

                projectChanges.Documents.Add(documentChanges);
            }

            result.Add(projectChanges);
        }

        return result;
    }
}

public abstract class NotifiableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


public class ProjectChangesViewModel : NotifiableBase
{
    private readonly ProjectChanges _projectChanges;
    public ProjectChangesViewModel(ProjectChanges projectChanges)
    {
        _projectChanges = projectChanges;
        _id = projectChanges.Id;
        _documents = new(projectChanges.Documents.Select(x => new DocumentChangesViewModel(x)));
        foreach (var item in _documents)
        {
            item.PropertyChanged += DocumentPropertyChanged;
        }
    }

    private void DocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not DocumentChangesViewModel documentChangesViewModel)
        {
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(documentChangesViewModel.HasSelectedChanges)))
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

    private ObservableCollection<DocumentChangesViewModel> _documents = [];
    public ObservableCollection<DocumentChangesViewModel> Documents
    {
        get => _documents;
        set
        {
            if (_documents != value)
            {
                _documents = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasSelectedChanges => _documents.Any(x => x.HasSelectedChanges);
}

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