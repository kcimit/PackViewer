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
            if (e.Args.Length == 0)
            {
                MessageBox.Show("No image file specified");
                Environment.Exit(0);
            }
            MainWindow wnd = new MainWindow(e.Args[0]);
            wnd.Show();
        }
    }
}
