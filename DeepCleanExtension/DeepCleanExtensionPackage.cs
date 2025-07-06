using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace DeepCleanExtension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(AboutToolWindow))]
    public sealed class DeepCleanExtensionPackage : AsyncPackage
    {
        /// <summary>
        /// DeepCleanExtensionPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "d71b1f83-740a-49fb-be25-c0a3d62f1dd9";

        private const int AboutToolWindowCommandId = 0x200;

        private static readonly Guid CommandSet = new("63eb71c3-4953-4295-9b6c-8549f2ff0abf"); // Your command set GUID

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await DeepCleanCommand.InitializeAsync(this);
            // Register the command for About tool window
            OleMenuCommandService commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            CommandID menuCommandID = new(CommandSet, AboutToolWindowCommandId);
            OleMenuCommand menuItem = new(ShowToolWindow, menuCommandID);
            commandService?.AddCommand(menuItem);
        }

        private void ShowToolWindow(object sender, EventArgs e)
        {
            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                ToolWindowPane window = await ShowToolWindowAsync(typeof(AboutToolWindow), 0, true, DisposalToken);
                if ((window?.Frame) == null)
                {
                    throw new NotSupportedException("Cannot create tool window.");
                }
            });
        }

        #endregion
    }
}