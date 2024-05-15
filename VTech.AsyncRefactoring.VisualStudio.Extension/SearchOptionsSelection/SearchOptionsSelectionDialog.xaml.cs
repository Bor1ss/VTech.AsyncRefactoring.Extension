using System.Windows;

using Microsoft.VisualStudio.PlatformUI;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.SearchOptionsSelection;
/// <summary>
/// Interaction logic for SearchOptionsSelectionDialog.xaml
/// </summary>
public partial class SearchOptionsSelectionDialog : DialogWindow
{
    public SearchOptionsSelectionDialog()
    {
        InitializeComponent();
        Context = new SearchOptionsSelectionContextViewModel();
        DataContext = Context;
    }

    public SearchOptionsSelectionContextViewModel Context { get; private set; }

    private void BtnApplyClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
