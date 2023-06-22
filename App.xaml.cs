using System;
using System.Windows;

namespace PackViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var input = string.Empty;
            if (e.Args.Length == 0)
            {
                MessageBox.Show("No image file specified");
                Environment.Exit(0);
            }

            /*if (e.Args.Length == 0)
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select starting folder with images",
                    ShowNewFolderButton=false
                })
                {
                    var result = dialog.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        input = dialog.SelectedPath;
                    }
                    else
                    {
                        MessageBox.Show("No image file specified");
                        Environment.Exit(0);
                    }
                }
            }
            else*/
            input = e.Args[0];

            var wnd = new MainWindow(input);
            wnd.Show();
        }
    }
}
