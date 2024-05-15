using VTech.AsyncRefactoring.VisualStudio.Extension.Shared;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.SearchOptionsSelection;
public class SearchOptionsSelectionContextViewModel : NotifiableBase
{
    private StartMethodSelectionType _startMethodSelectionType = StartMethodSelectionType.All;
    public StartMethodSelectionType StartMethodSelectionType
    {
        get => _startMethodSelectionType;
        set
        {
            if (_startMethodSelectionType != value)
            {
                _startMethodSelectionType = value;
                OnPropertyChanged();
            }
        }
    }

    public SearchOptions GetResult()
    {
        return new SearchOptions
        {
            StartMethodSelectionType = StartMethodSelectionType,
        };
    }
}
