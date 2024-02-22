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

namespace VTech.AsyncRefactoring.VisualStudio.Extension;
/// <summary>
/// Command handler
/// </summary>
internal sealed class FindAndFixAsyncIssuesCommand
{
    /// <summary>
    /// Command ID.
    /// </summary>
    public const int CommandId = 0x0100;

    /// <summary>
    /// Command menu group (command set GUID).
    /// </summary>
    public static readonly Guid CommandSet = new Guid("ee07953a-bfcf-4e38-8927-a2bf4e0a8eb0");

    /// <summary>
    /// VS Package that provides this command, not null.
    /// </summary>
    private readonly AsyncPackage package;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindAndFixAsyncIssuesCommand"/> class.
    /// Adds our command handlers for menu (commands must exist in the command table file)
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    /// <param name="commandService">Command service to add command to, not null.</param>
    private FindAndFixAsyncIssuesCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        this.package = package ?? throw new ArgumentNullException(nameof(package));
        commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

        var menuCommandID = new CommandID(CommandSet, CommandId);
        var menuItem = new MenuCommand(this.Execute, menuCommandID);
        commandService.AddCommand(menuItem);
    }

    /// <summary>
    /// Gets the instance of the command.
    /// </summary>
    public static FindAndFixAsyncIssuesCommand Instance
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets the service provider from the owner package.
    /// </summary>
    private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
    {
        get
        {
            return this.package;
        }
    }

    /// <summary>
    /// Initializes the singleton instance of the command.
    /// </summary>
    /// <param name="package">Owner package, not null.</param>
    public static async Task InitializeAsync(AsyncPackage package)
    {
        // Switch to the main thread - the call to AddCommand in FindAndFixAsyncIssuesCommand's constructor requires
        // the UI thread.
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        Instance = new FindAndFixAsyncIssuesCommand(package, commandService);


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

        string fileName = dte.Solution.FileName;

        ProjectItem activeProjectItem = activeDoc.ProjectItem;

        string projectId = activeProjectItem.Name;
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

        ExecuteAsync(fileName, methodSelector);
    }

    private async Task ExecuteAsync(string solutionPath, IMethodSelector methodSelector)
    {
        string title = "Success";
        string msg = "done";
        try
        {
            AsyncronizationProcessor asyncronizationProcessor = new(solutionPath);
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

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

        VsShellUtilities.ShowMessageBox(
            this.package,
            msg,
            title,
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
