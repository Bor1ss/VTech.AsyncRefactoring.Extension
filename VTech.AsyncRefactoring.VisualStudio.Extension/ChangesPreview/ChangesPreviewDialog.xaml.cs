using System.Collections.Generic;
using System.Windows;

using Microsoft.VisualStudio.PlatformUI;

using VTech.AsyncRefactoring.Base.Changes;

namespace VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;

public partial class ChangesPreviewDialog : DialogWindow
{
    public ChangesPreviewDialog(List<ProjectChanges> changes)
    {
        InitializeComponent();
        Context = new ChangesPreviewContextViewModel(changes);
        DataContext = Context;
    }

    public ChangesPreviewContextViewModel Context { get; private set; }

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
