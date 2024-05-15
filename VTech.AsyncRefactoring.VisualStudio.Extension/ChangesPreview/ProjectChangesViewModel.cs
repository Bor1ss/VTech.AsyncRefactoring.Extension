using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using VTech.AsyncRefactoring.Base.Changes;
using VTech.AsyncRefactoring.VisualStudio.Extension.Shared;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;

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
