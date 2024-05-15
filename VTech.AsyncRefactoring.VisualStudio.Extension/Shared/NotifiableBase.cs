using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.Shared;

public abstract class NotifiableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
