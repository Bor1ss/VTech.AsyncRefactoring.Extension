using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using EnvDTE;
using EnvDTE80;

using Task = System.Threading.Tasks.Task;
using VTech.AsyncRefactoring.Base;
using VTech.AsyncRefactoring.Base.MethodSelector;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.ComponentModelHost;

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
        // Switch to the main thread - the call to AddCommand in FindAndFixAsyncIssuesCommand's constructor requires
        // the UI thread.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

        IComponentModel componentModel = (IComponentModel)(await package.GetServiceAsync(typeof(SComponentModel)));
        VisualStudioWorkspace visualStudioWorkspace = componentModel.GetService<VisualStudioWorkspace>();

        Instance = new FindAndFixAsyncIssuesCommand(package, commandService, visualStudioWorkspace);
    }



    /// <summary>
    /// This function is the callback used to execute the command when the menu item is clicked.
    /// See the constructor to see how the menu item is associated with this function using
    /// OleMenuCommandService service and MenuCommand class.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event args.</param>
    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        DTE2 dte = ServiceProvider.GetServiceAsync(typeof(DTE)).Result as DTE2 ?? throw new Exception("DTE2 not found");
        Document activeDoc = dte.ActiveDocument;

        if(activeDoc is null || !string.Equals(activeDoc.Language, "csharp", StringComparison.CurrentCultureIgnoreCase))
        {
            throw new Exception("ActiveDocument not found");
        }

        string projectId = activeDoc.ProjectItem.ContainingProject.Name;
        string codeFileId = dte.ActiveDocument.Name;

        IMethodSelector methodSelector;

        if (dte.ActiveDocument.Selection is not TextSelection textSelection)
        {
            methodSelector = new FileRelatedMethodSelector(projectId, codeFileId);
        }
        else
        {
            methodSelector = new CoursorRelatedMethodSelector(projectId, codeFileId, textSelection.CurrentLine, textSelection.CurrentColumn);
        }

        ExecuteAsync(methodSelector);
    }

    private async Task ExecuteAsync(IMethodSelector methodSelector)
    {
        string title = "Success";
        string msg = "done";
        try
        {
            AsyncronizationProcessor asyncronizationProcessor = new(_visualStudioWorkspace.CurrentSolution);
            await asyncronizationProcessor.InitializeCodeMapAsync();

            var changes = asyncronizationProcessor.CollectSuggestedChanges(methodSelector);

            //dialog for selection

            await asyncronizationProcessor.ApplyChangesAsync(changes);
        }
        catch (Exception ex)
        {
            title = "error";
            msg = ex.Message;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

        VsShellUtilities.ShowMessageBox(
            this._package,
            msg,
            title,
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
