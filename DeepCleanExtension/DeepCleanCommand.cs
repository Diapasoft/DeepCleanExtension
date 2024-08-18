using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

#nullable enable

namespace DeepCleanExtension
{
    /// <summary>
    /// Command handler.
    /// </summary>
    internal sealed class DeepCleanCommand
    {
        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DeepCleanCommand Instance { get; private set; } = null!;

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IAsyncServiceProvider ServiceProvider => package;

        public const int Command01 = 0x0101;

        public const int Command02 = 0x0102;

        public const int Command03 = 0x0103;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new("63eb71c3-4953-4295-9b6c-8549f2ff0abf");

        private readonly VSExtensionHelper extensionHelper;

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeepCleanCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DeepCleanCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            extensionHelper = new(package);

            CommandID command_CleanAllSolution = new(CommandSet, Command01);
            MenuCommand menu01 = new((_, _) => CleanAllSolutionDirectories(), command_CleanAllSolution);
            commandService.AddCommand(menu01);

            CommandID command_CleanAllProject = new(CommandSet, Command02);
            MenuCommand menu02 = new((_, _) => CleanAllProjectDirectories(), command_CleanAllProject);
            commandService.AddCommand(menu02);

            CommandID command_CleanSelected = new(CommandSet, Command03);
            MenuCommand menu03 = new((_, _) => SelectDirectoriesAndClean(), command_CleanSelected);
            commandService.AddCommand(menu03);
        }

        #region Commands

        private void CleanAllProjectDirectories()
        {
            if (MessageBox.Show("Confirm deleting all 'bin' and 'obj' directories in current Project?", nameof(DeepCleanExtension), MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }
            string? projectPath = extensionHelper.GetCurrentOpenVSProjectPath();
            Run(new AllDirectorySelector(), projectPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for all Project directories.");
        }

        private void CleanAllSolutionDirectories()
        {
            if (MessageBox.Show("Confirm deleting all 'bin' and 'obj' directories in current Solution?", nameof(DeepCleanExtension), MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }
            string? solutionPath = extensionHelper.GetCurrentOpenVSSolutionPath();
            Run(new AllDirectorySelector(), solutionPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for all Solution directories.");
        }

        private void SelectDirectoriesAndClean()
        {
            string? solutionPath = extensionHelper.GetCurrentOpenVSSolutionPath();
            Run(new DirectorySelectorByUser(), solutionPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for selected directories.");
        }

        #endregion

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in Command1's constructor requires the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = (OleMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            Instance = new DeepCleanCommand(package, commandService);
        }

        /// <summary>
        /// Shows "No open Solution found" message.
        /// </summary>
        private void NoSolutionFound()
        {
            MessageBox.Show($"{nameof(DeepCleanExtension)} was unable to get current Solution / Project.", nameof(DeepCleanExtension));
        }

        private void Run(IDirectorySelector directorySelector, string? path)
        {
            if (string.IsNullOrEmpty(path) || path is null)
            {
                NoSolutionFound();
                return;
            }
            IList<DirectoryInfo> list = directorySelector.GetSelectedDirectories(path);
            foreach (DirectoryInfo dir in list)
            {
                dir.Delete(true);
            }
        }
    }
}