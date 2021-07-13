using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select starting image folder",
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
            else
                input = e.Args[0];
            MainWindow wnd = new MainWindow(input);
            wnd.Show();
        }
    }
}
