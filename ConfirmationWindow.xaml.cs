using System.Windows;

namespace PackViewer
{
    public partial class ConfirmationWindow : Window
    {
        public bool DeleteFolders { get; private set; }
        public bool SaveFolders { get; private set; }
        public bool RemoveOriginalFolders { get; private set; }

        public ConfirmationWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteFolders = DeleteFoldersCheckBox.IsChecked == true;
            SaveFolders = SaveFoldersCheckBox.IsChecked == true;
            RemoveOriginalFolders = RemoveOriginalFoldersCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void DoNothingButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteFolders = false;
            SaveFolders = false;
            RemoveOriginalFolders = false;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
