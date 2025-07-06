using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;

namespace DeepCleanExtension
{
    [Guid("ADD35176-CF67-4DF1-87A6-417C87C4D75D")] // Generate a new GUID for your tool window
    public sealed class AboutToolWindow : ToolWindowPane
    {
        public AboutToolWindow() : base(null)
        {
            Caption = "Deep Clean - About";
            Content = new AboutToolWindowControl();
        }
    }
}