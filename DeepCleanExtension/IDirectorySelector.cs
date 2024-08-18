using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable

namespace DeepCleanExtension
{
    #region Interface and Base Class

    /// <summary>
    /// Represent a service to select which directories have to be deleted.
    /// </summary>
    internal interface IDirectorySelector
    {
        public IList<DirectoryInfo> GetSelectedDirectories(string path);
    }

    internal abstract class DirectorySelector : IDirectorySelector
    {
        private readonly static string[] directoriesToDelete = ["bin", "obj"];

        public abstract IList<DirectoryInfo> GetSelectedDirectories(string path);

        protected List<DirectoryInfo> GetProjectDirectories(string path)
        {
            string solutionDir = new FileInfo(path).Directory.FullName;
            List<DirectoryInfo> result = new();
            IEnumerable<DirectoryInfo> foundDirectories = new DirectoryInfo(solutionDir).GetDirectories();
            foreach (DirectoryInfo foundDirectory in foundDirectories)
            {
                IEnumerable<DirectoryInfo> binAndObjDirectories = foundDirectory.GetDirectories().Where(x => directoriesToDelete.Contains(x.Name.ToLower()));
                result.AddRange(binAndObjDirectories);
            }
            return result;
        }
    }

    #endregion

    internal class AllDirectorySelector : DirectorySelector
    {
        public override IList<DirectoryInfo> GetSelectedDirectories(string path)
        {
            List<DirectoryInfo> projectDirectories = GetProjectDirectories(path);
            return projectDirectories;
        }
    }

    internal class DirectorySelectorByUser : DirectorySelector
    {
        public override IList<DirectoryInfo> GetSelectedDirectories(string path)
        {
            List<DirectoryInfo> projectDirectories = GetProjectDirectories(path);
            List<DirectoryInfo> selectedDirectories = new();
            foreach (DirectoryInfo dir in projectDirectories)
            {
                if (MessageBox.Show($"Confirm cleaning directory '{dir.FullName}' ?", nameof(DeepCleanExtension), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    selectedDirectories.Add(dir);
                }
            }
            return selectedDirectories;
        }
    }
}