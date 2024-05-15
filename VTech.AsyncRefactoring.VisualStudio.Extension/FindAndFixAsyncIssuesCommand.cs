using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading.Tasks;

using EnvDTE;

using EnvDTE80;

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using VTech.AsyncRefactoring.Base;
using VTech.AsyncRefactoring.Base.MethodSelector;
using VTech.AsyncRefactoring.VisualStudio.Extension.ChangesPreview;
using VTech.AsyncRefactoring.VisualStudio.Extension.SearchOptionsSelection;

using Task = System.Threading.Tasks.Task;

namespace VTech.AsyncRefactoring.VisualStudio.Extension;

internal sealed class FindAndFixAsyncIssuesCommand
{
    public const int COMMAND_ID = 0x0100;

    public static readonly Guid CommandSet = new("ee07953a-bfcf-4e38-8927-a2bf4e0a8eb0");

    private readonly AsyncPackage _package;
    private readonly VisualStudioWorkspace _visualStudioWorkspace;

    private FindAndFixAsyncIssuesCommand(AsyncPackage package, OleMenuCommandService commandService, VisualStudioWorkspace visualStudioWorkspace)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));
        _visualStudioWorkspace = visualStudioWorkspace ?? throw new ArgumentNullException(nameof(visualStudioWorkspace));
        commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

        var menuCommandID = new CommandID(CommandSet, COMMAND_ID);
        var menuItem = new MenuCommand(this.Execute, menuCommandID);
        commandService.AddCommand(menuItem);
    }

    public static FindAndFixAsyncIssuesCommand Instance { get; private set; }

    private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider => _package;

    public static async Task InitializeAsync(AsyncPackage package)
    {
        // Switch to the main thread - the call to AddCommand in FindAndFixAsyncIssuesCommand's constructor requires the UI thread.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

        IComponentModel componentModel = (IComponentModel)(await package.GetServiceAsync(typeof(SComponentModel)));
        VisualStudioWorkspace visualStudioWorkspace = componentModel.GetService<VisualStudioWorkspace>();

        Instance = new FindAndFixAsyncIssuesCommand(package, commandService, visualStudioWorkspace);
    }

    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        DTE2 dte = ServiceProvider.GetServiceAsync(typeof(DTE)).Result as DTE2 ?? throw new Exception("DTE2 not found");
        Document activeDoc = dte.ActiveDocument;

        if (activeDoc is null || !string.Equals(activeDoc.Language, "csharp", StringComparison.CurrentCultureIgnoreCase))
        {
            throw new Exception("ActiveDocument not found");
        }

        SearchOptionsSelectionDialog dialog = new();
        bool? optionsSelected = dialog.ShowDialog();

        if (optionsSelected != true)
        {
            return;
        }

        SearchOptions searchOptions = dialog.Context.GetResult();

        string projectId = activeDoc.ProjectItem.ContainingProject.Name;
        string codeFileId = dte.ActiveDocument.Name;
        IMethodSelector methodSelector = searchOptions.StartMethodSelectionType switch
        {
            StartMethodSelectionType.All => new AllMethodSelector(),
            StartMethodSelectionType.Project => new ProjectRelatedMethodSelector(projectId),
            StartMethodSelectionType.Document => new FileRelatedMethodSelector(projectId, codeFileId),
            StartMethodSelectionType.Selection when dte.ActiveDocument.Selection is not TextSelection => new FileRelatedMethodSelector(projectId, codeFileId),
            StartMethodSelectionType.Selection when dte.ActiveDocument.Selection is TextSelection textSelection => new CoursorRelatedMethodSelector(projectId, codeFileId, textSelection.CurrentLine),
            _ => throw new ArgumentOutOfRangeException()
        };

        ExecuteAsync(methodSelector).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(IMethodSelector methodSelector)
    {
        await Task.Yield();

        try
        {
            //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            ////show spinner

            await TaskScheduler.Default;

            AsyncronizationProcessor asyncronizationProcessor = new(_visualStudioWorkspace.CurrentSolution);
            await asyncronizationProcessor.InitializeCodeMapAsync();

            List<Base.Changes.ProjectChanges> changes = asyncronizationProcessor.CollectSuggestedChanges(methodSelector);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            if (changes.Count == 0)
            {
                ShowMessage("Info", "No issues found.");

                return;
            }

            ChangesPreviewDialog dialog = new(changes);
            bool? changesConfirmed = dialog.ShowDialog();

            if (changesConfirmed != true)
            {
                return;
            }

            List<Base.Changes.ProjectChanges> selectedChanges = dialog.Context.GetSelectedChanges();

            await asyncronizationProcessor.ApplyChangesAsync(selectedChanges);

            ShowMessage("Success", "Changes successfully applied!");
        }
        catch (Exception ex)
        {
            ShowMessage("error", ex.Message);
        }
    }

    private void ShowMessage(string title, string message)
    {
        VsShellUtilities.ShowMessageBox(
            _package,
            message,
            title,
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
