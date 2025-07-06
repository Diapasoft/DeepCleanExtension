using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Environment;

#nullable enable

namespace DeepCleanExtension
{
    internal sealed class DeepCleanCommand
    {
        public static DeepCleanCommand Instance { get; private set; } = null!;

        /// <summary>
        /// Build after cleanup is currently disabled. A customizable settings will be added in future releases.
        /// </summary>
        public bool EnableBuild { get; } = false;

        private IAsyncServiceProvider ServiceProvider => _package;

        //public const int Command01 = 0x0101;

        //public const int Command02 = 0x0102;

        //public const int Command03 = 0x0103;

        public const int c01_CleanupSelectedProjects = 0x0101;

        public const int c02_CleanupEntireSolution = 0x0102;

        public static readonly Guid CommandSet = new("63eb71c3-4953-4295-9b6c-8549f2ff0abf");

        private readonly AsyncPackage _package;

        private readonly VSExtensionHelper extensionHelper;

        private IVsOutputWindowPane? _outputPane;

        private DeepCleanCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            extensionHelper = new(package);

            // Initialize our own Output Window pane
            _ = InitializeOutputPaneAsync();

            // Register commands
            //commandService.AddCommand(new MenuCommand((_, _) => CleanAllSolutionDirectories(), new CommandID(CommandSet, Command01)));
            //commandService.AddCommand(new MenuCommand((_, _) => CleanAllProjectDirectories(), new CommandID(CommandSet, Command02)));
            //commandService.AddCommand(new MenuCommand((_, _) => SelectDirectoriesAndClean(), new CommandID(CommandSet, Command03)));

            // Command01: Deep clean single project
            OleMenuCommand c01 = new((_, _) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    try
                    {
                        await CleanSelectedProjectAsync();
                    }
                    catch (Exception ex)
                    {
                        await LogAsync($"Error in CleanSelectedProjectAsync: {ex}");
                    }
                });
            }, new CommandID(CommandSet, c01_CleanupSelectedProjects));
            c01.BeforeQueryStatus += (_, e) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    bool isProjectSelected = false;
                    if (Package.GetGlobalService(typeof(DTE)) is DTE2 dte && dte.ToolWindows?.SolutionExplorer?.SelectedItems is Array items && items.Length > 0 && items.GetValue(0) is UIHierarchyItem uiItem && uiItem.Object is Project)
                    {
                        isProjectSelected = true;
                    }
                    c01.Visible = isProjectSelected;
                    c01.Enabled = isProjectSelected;
                });
            };
            commandService.AddCommand(c01);

            // Command02: Deep Clean All Projects in Solution
            OleMenuCommand c02 = new((_, _) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    try
                    {
                        await CleanAllSolutionProjectsAsync();
                    }
                    catch (Exception ex)
                    {
                        await LogAsync($"Error in CleanAllSolutionProjectsAsync: {ex}");
                    }
                });
            }, new CommandID(CommandSet, c02_CleanupEntireSolution));
            c02.BeforeQueryStatus += (_, __) =>
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    DTE2? dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
                    bool isSolutionSelected = dte?.ToolWindows?.SolutionExplorer?.SelectedItems is Array items && items.Length == 1 && items.GetValue(0) is UIHierarchyItem hi && hi.Object is Solution;
                    c02.Visible = c02.Enabled = isSolutionSelected;
                });
            };
            commandService.AddCommand(c02);
        }

        #region new

        private async Task CleanAllSolutionProjectsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LogAsync("Starting CleanAllSolutionProjectsAsync…");

            // 1) Get DTE and all projects
            DTE2? dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte?.Solution?.Projects is not Projects projs || projs.Count == 0)
            {
                await LogAsync("No projects in solution. Aborting.");
                MessageBox.Show("Solution contains no projects.");
                return;
            }

            // 0) Get current startup project(s)
            object? startup = dte.Solution?.SolutionBuild?.StartupProjects;
            string[]? startupProjects = startup switch
            {
                string s => [s],
                object[] arr => arr.OfType<string>().ToArray(),
                _ => null
            };

            // 2) Collect them into a list(to avoid COM enumeration quirks.
            List<Project> list = [];
            foreach (Project p in projs)
            {
                // Ignore DteMiscProject projects (not user's project).
                Type projectType = p.GetType();
                if (projectType.FullName == "Microsoft.VisualStudio.CommonIDE.Solutions.Dte.DteMiscProject")
                {
                    continue;
                }

                if (p.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    // Drill into solution folder.
                    foreach (ProjectItem pi in p.ProjectItems)
                    {
                        if (pi.SubProject is Project sp && !string.IsNullOrEmpty(sp.FullName) && File.Exists(sp.FullName))
                        {
                            list.Add(sp);
                        }
                    }
                }
                else
                {
                    list.Add(p);
                }
            }

            // 3) For each project, call your per‑project cleanup
            foreach (Project project in list)
            {
                Type a = project.GetType();
                await LogAsync($"=== Solution‑wide: processing {project.Name} ===");
                await CleanProjectAsync(project);
                // Restore startup project if needed
                if (dte.Solution != null && startupProjects?.Contains(project.UniqueName, StringComparer.OrdinalIgnoreCase) == true)
                {
                    dte.Solution.SolutionBuild.StartupProjects = startupProjects.Length == 1
                        ? project.UniqueName
                        : startupProjects.Cast<object>().ToArray(); // Handles multi-startup projects

                    await LogAsync("Startup project setting restored.");
                }
            }

            extensionHelper.WriteStausBar("Deep Clean completed for all solution projects.");
            await LogAsync("Finished CleanAllSolutionProjectsAsync.");
        }

        private async Task CleanProjectAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LogAsync($"--- Processing project: {project.Name} ---");

            // Resolve paths
            string projectPath = project.FullName;
            string? projectDir = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrEmpty(projectPath) || projectDir == null || !File.Exists(projectPath))
            {
                await LogAsync($"Invalid project path: {projectPath}");
                MessageBox.Show($"Could not determine project path for '{project.Name}'.");
                return;
            }

            // Get IVsSolution & hierarchy
            if (await ServiceProvider.GetServiceAsync(typeof(SVsSolution)) is not IVsSolution vsSolution || vsSolution.GetProjectOfUniqueName(project.UniqueName, out IVsHierarchy hierarchy) != VSConstants.S_OK || hierarchy == null)
            {
                await LogAsync($"Unable to locate project in IVsSolution: {project.Name}");
                MessageBox.Show($"Unable to locate project in solution for '{project.Name}'.");
                return;
            }

            // Retrieve project GUID
            int hrGuid = vsSolution.GetGuidOfProject(hierarchy, out Guid projectGuid);
            if (hrGuid != VSConstants.S_OK || projectGuid == Guid.Empty)
            {
                await LogAsync($"GetGuidOfProject failed (hr=0x{hrGuid:X8}).");
                MessageBox.Show($"Failed to retrieve project GUID for '{project.Name}'.");
                return;
            }
            await LogAsync($"Project GUID: {projectGuid}");

            // Unload
            IVsSolution4 vsSol4 = (IVsSolution4)vsSolution;
            if (vsSol4.UnloadProject(ref projectGuid, (uint)_VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser) != VSConstants.S_OK)
            {
                await LogAsync("UnloadProject failed.");
                MessageBox.Show($"Failed to unload '{project.Name}'.");
                return;
            }
            await LogAsync("Project unloaded.");

            // Delete bin/obj
            await Task.WhenAll(
                TryDeleteDirectoryAsync(Path.Combine(projectDir, "bin")),
                TryDeleteDirectoryAsync(Path.Combine(projectDir, "obj"))
            );
            await LogAsync("Deleted bin/obj directories.");

            // Reload
            if (vsSol4.ReloadProject(ref projectGuid) != VSConstants.S_OK)
            {
                await LogAsync("ReloadProject failed.");
                MessageBox.Show($"Failed to reload '{project.Name}'.");
                return;
            }
            await LogAsync("Project reloaded.");

            // Select & build
            if (EnableBuild)
            {
                // select the node so Build.BuildSelection targets it
                UIHierarchyItem? uiItem = await FindHierarchyItemForProjectAsync(project);
                uiItem?.Select(vsUISelectionType.vsUISelectionTypeSelect);
                await LogAsync("Project node selected.");
                DTE2? dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
                dte?.ExecuteCommand("Build.BuildSelection");
                await LogAsync("Build.BuildSelection invoked.");
            }
        }

        private async Task CleanSelectedProjectAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await LogAsync("Starting CleanSelectedProjectAsync…");

            // 1) Get DTE and selection
            DTE2? dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte?.ToolWindows?.SolutionExplorer?.SelectedItems is not Array selItems || selItems.Length == 0)
            {
                await LogAsync("No items selected. Aborting.");
                MessageBox.Show("No projects are selected.");
                return;
            }

            // 2) Process each selected item
            foreach (object? sel in selItems)
            {
                if (sel is not UIHierarchyItem selItem || selItem.Object is not Project project)
                {
                    await LogAsync("Skipped non-project item.");
                    continue;
                }
                await CleanProjectAsync(project);
            }

            extensionHelper.WriteStausBar("Deep Clean completed for selected project(s).");
            await LogAsync("Finished CleanSelectedProjectAsync for all selections.");
        }

        /// <summary>
        /// Helper to locate the UIHierarchyItem for a given Project in Solution Explorer.
        /// </summary>
        private async Task<UIHierarchyItem?> FindHierarchyItemForProjectAsync(Project project)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DTE2? dte = await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            });
            if (dte?.ToolWindows?.SolutionExplorer?.SelectedItems is not Array items)
            {
                return null;
            }
            foreach (UIHierarchyItem item in items)
            {
                if (item.Object is Project p && p.UniqueName == project.UniqueName)
                {
                    return item;
                }
            }
            // fallback: first selected
            return items.Length > 0 ? items.GetValue(0) as UIHierarchyItem : null;
        }

        private async Task TryDeleteDirectoryAsync(string? path)
        {
            if (path != null && Directory.Exists(path))
            {
                try
                {
                    await Task.Run(() => Directory.Delete(path, true));
                    await LogAsync($"Deleted directory: {path}");
                }
                catch (Exception ex)
                {
                    await LogAsync($"Error deleting {path}: {ex.Message}");
                }
            }
        }

        #endregion

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService commandService = (OleMenuCommandService)await package.GetServiceAsync(typeof(IMenuCommandService));
            Instance = new DeepCleanCommand(package, commandService);
        }

        private async Task InitializeOutputPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            Guid paneGuid = new("E0F5A2B1-4B2C-4E5A-A7D6-123456789ABC");
            if (await ServiceProvider.GetServiceAsync(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow)
            {
                outWindow.CreatePane(ref paneGuid, "DeepCleanExtension", 1, 1);
                outWindow.GetPane(ref paneGuid, out _outputPane);
            }
            else
            {
                await LogAsync($"Warning: failed to initialize {nameof(IVsOutputWindow)} for {nameof(DeepCleanExtension)}");
            }
            await LogAsync("=== DeepCleanExtension initialized ===");
        }

        private async Task LogAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _outputPane?.OutputString($"{DateTime.Now:HH:mm:ss}  {message}{NewLine}");
        }

        private void NoSolutionOrProjectFound(string addText = "")
        {
            MessageBox.Show($"{nameof(DeepCleanExtension)} was unable to get current Solution / Project.{NewLine}{addText}", nameof(DeepCleanExtension));
            _ = LogAsync("No solution or project found.");
        }

        #region Core Commands

        private void CleanAllProjectDirectories()
        {
            if (MessageBox.Show("Confirm deleting all 'bin' and 'obj' directories in current Project?", nameof(DeepCleanExtension), MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }

            if (!extensionHelper.TryGetCurrentOpenVSProjectPath(out string projectPath))
            {
                NoSolutionOrProjectFound("Try opening a file from the project you want to deep clean.");
                return;
            }
            RunCommand(new AllDirectorySelector(), projectPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for all Project directories.");
            _ = LogAsync($"CleanAllProjectDirectories on {projectPath}");
        }

        private void CleanAllSolutionDirectories()
        {
            if (MessageBox.Show("Confirm deleting all 'bin' and 'obj' directories in current Solution?", nameof(DeepCleanExtension), MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                return;
            }

            if (!extensionHelper.TryGetCurrentOpenVSSolutionPath(out string solutionPath))
            {
                NoSolutionOrProjectFound();
                return;
            }
            RunCommand(new AllDirectorySelector(), solutionPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for all Solution directories.");
            _ = LogAsync($"CleanAllSolutionDirectories on {solutionPath}");
        }

        private void SelectDirectoriesAndClean()
        {
            if (!extensionHelper.TryGetCurrentOpenVSSolutionPath(out string solutionPath))
            {
                NoSolutionOrProjectFound();
                return;
            }
            RunCommand(new DirectorySelectorByUser(), solutionPath);
            extensionHelper.WriteStausBar("Deep Clean command completed for selected directories.");
            _ = LogAsync($"SelectDirectoriesAndClean on {solutionPath}");
        }

        #endregion

        private void RunCommand(IDirectorySelector directorySelector, string path)
        {
            IEnumerable<DirectoryInfo> list = directorySelector.GetSelectedDirectories(path);
            foreach (DirectoryInfo dir in list)
            {
                dir.Delete(true);
            }
        }
    }
}