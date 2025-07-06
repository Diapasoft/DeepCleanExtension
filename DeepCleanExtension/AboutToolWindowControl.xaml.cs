#nullable enable

using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DeepCleanExtension
{
    /// <summary>
    /// Interaction logic for AboutToolWindowControl.xaml
    /// </summary>
    public partial class AboutToolWindowControl : UserControl
    {
        public AboutToolWindowControl()
        {
            InitializeComponent();
            tbVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}