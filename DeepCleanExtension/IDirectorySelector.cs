using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable

namespace DeepCleanExtension
{
    #region Interface and Base Class

    internal interface IDirectorySelector
    {
        public IList<DirectoryInfo> GetSelectedDirectories(string path);
    }

    internal class DirectorySelectorBase
    {
        private readonly static string[] allowedDirectories = ["bin", "obj"];

        protected List<DirectoryInfo> GetProjectDirectories(string path)
        {
            string solutionDir = new FileInfo(path).Directory.FullName;
            List<DirectoryInfo> result = new();
            IEnumerable<DirectoryInfo> foundDirectories = new DirectoryInfo(solutionDir).GetDirectories();
            foreach (DirectoryInfo foundDirectory in foundDirectories)
            {
                List<DirectoryInfo> binAndObjDirectories = foundDirectory.GetDirectories().Where(x => allowedDirectories.Contains(x.Name.ToLower())).ToList();
                result.AddRange(binAndObjDirectories);
            }
            return result;
        }
    }

    #endregion

    internal class AllDirectorySelector : DirectorySelectorBase, IDirectorySelector
    {
        public IList<DirectoryInfo> GetSelectedDirectories(string path)
        {
            List<DirectoryInfo> projectDirectories = GetProjectDirectories(path);
            return projectDirectories;
        }
    }

    internal class DirectorySelectorByUser : DirectorySelectorBase, IDirectorySelector
    {
        public IList<DirectoryInfo> GetSelectedDirectories(string path)
        {
            List<DirectoryInfo> projectDirectories = GetProjectDirectories(path);
            List<DirectoryInfo> selectedDirectories = new();
            foreach (DirectoryInfo dir in projectDirectories)
            {
                if (MessageBox.Show($"Confirm cleaning directory '{dir.FullName}' ?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    selectedDirectories.Add(dir);
                }
            }
            return selectedDirectories;
        }
    }
}