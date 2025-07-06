using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;

#nullable enable

namespace DeepCleanExtension
{
    public interface IVSExtensionHelper
    {
        public bool TryGetCurrentOpenVSProjectPath(out string projectPath);

        public bool TryGetCurrentOpenVSSolutionPath(out string solutionPath);

        public void WriteStausBar(string text);
    }

    public class VSExtensionHelper(AsyncPackage package) : IVSExtensionHelper
    {
        public bool TryGetCurrentOpenVSProjectPath(out string projectPath)
        {
            // Ensure we are on UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            projectPath = string.Empty;

            object? selectedObject = GetVSSelectedObject();
            if (selectedObject is ProjectItem projectItem)
            {
                projectPath = projectItem.DTE.FullName;
                return true;
            }
            return false;
        }

        public bool TryGetCurrentOpenVSSolutionPath(out string solutionPath)
        {
            // Ensure we are on UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            solutionPath = string.Empty;

            object? selectedObject = GetVSSelectedObject();
            // Try to get selected project item.
            if (selectedObject is ProjectItem document)
            {
                solutionPath = document.DTE.Solution.FullName;
                return true;
            }
            // Try by checking global service of type IVsSolution.
            else if (Package.GetGlobalService(typeof(IVsSolution)) is IVsSolution solution)
            {
                solution.GetSolutionInfo(out string solutionDirectory, out string solutionName, out string solutionDirectory2);
                DirectoryInfo solutionDirectoryInfo = new(solutionDirectory);
                solutionPath = solutionDirectoryInfo.FullName;
                return true;
            }
            return false;
        }

        public void WriteStausBar(string text)
        {
            // Ensure we are on UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get status bar service from current package.
            IVsStatusbar statusBar = package.GetService<SVsStatusbar, IVsStatusbar>();

            // Make sure the status bar is not frozen.
            statusBar.IsFrozen(out int frozen);
            if (frozen != 0)
            {
                statusBar.FreezeOutput(0);
            }

            // Set the status bar text and make its display static.
            statusBar.SetText(text);

            // Freeze the status bar.
            statusBar.FreezeOutput(1);
        }

        private object? GetVSSelectedObject()
        {
            // Ensure we are on UI thread.
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsMonitorSelection monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));
            monitorSelection.GetCurrentSelection(out IntPtr hierarchyPointer, out uint projectItemId, out IVsMultiItemSelect multiItemSelect, out IntPtr selectionContainerPointer);
            if (hierarchyPointer == IntPtr.Zero)
            {
                return null;
            }
            object? selectedObject = null;
            if (Marshal.GetTypedObjectForIUnknown(hierarchyPointer, typeof(IVsHierarchy)) is IVsHierarchy selectedHierarchy)
            {
                ErrorHandler.ThrowOnFailure(selectedHierarchy.GetProperty(projectItemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out selectedObject));
            }
            return selectedObject;
        }
    }
}