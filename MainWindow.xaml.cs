using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PackViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel PackView;

        readonly int FastForwardValue = 10;
        readonly bool enableLibRaw = false;
        List<string> filesInFolder;
        int currentIndex;
        string _file;
        private CancellationTokenSource tokenSource;
        private CancellationToken token;

        public Task foldersAsync { get; private set; }

        public MainWindow(string file)
        {
            InitializeComponent();

            PackView = new ViewModel();
            _file = file;
            enableLibRaw = ViewModel.IsRaw(file);
            DataContext = PackView;
            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;
        }
        private void ShowImage()
        {
            try
            {
                if (filesInFolder == null || currentIndex >= filesInFolder.Count)
                    return;

                var file = filesInFolder[currentIndex];
                ShowStatus();
                var numRetries = 15;
                byte[] BitmapStream = null;
                Rotation rot = Rotation.Rotate0;

                while (numRetries > 0)
                {
                    try
                    {
                        BitmapStream = PackView.GetImage(file, out rot);
                        break;
                    }
                    catch
                    {
                        numRetries--;
                        Thread.Sleep(200);
                    }
                }
                if (BitmapStream == null)
                    throw new Exception($"Cannot acces file {file}");

                if (ViewModel.IsRaw(file) && enableLibRaw)
                    ImageProcess.DecompressRaw(BitmapStream, DImage);
                else
                    ImageProcess.DecompressJpeg(BitmapStream, DImage, rot);

                BitmapStream = null;
                PackView.IsFav = PackView.GetFavStatus(file);
            }
            catch (Exception e) {
                MessageBox.Show(e.Message);
            }
        }

        private void ShowStatus()
        {
            if (filesInFolder != null)
                PackView.StatusBottom = $"({currentIndex+1}/{filesInFolder.Count}) [{PackView.CurrentFolderIndex + 1}/{PackView.FoldersCount}] {filesInFolder[currentIndex]}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.Key== Key.Escape)
                Close();
            if (e.Key == Key.Left)
                PrevImage();
            if (e.Key == Key.Right)
                NextImage();
            if (e.Key == Key.Up)
                FolderUp();
            if (e.Key == Key.Down)
                FolderDown();
            if (e.Key == Key.End)
                LastImage();
            if (e.Key == Key.Home)
                FirstImage();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Delete)
                ThrashIt();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Insert)
                SaveIt();
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.S)
                AddToFav();

            e.Handled = true;
        }
        private void FirstImage()
        {
            if (PackView != null)
            {
                currentIndex = 0;
                ShowImage();
            }
        }
        private void LastImage()
        {
            if (PackView != null)
            {
                currentIndex = filesInFolder.Count - 1;
                ShowImage();
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var source = DImage.Source;
            DImage.Source = null;
            var delete = false;
            var deleteOriginal = false;
            var cancel = false;
            var save = false;
                        
            var cntDel = PackView.FoldersThrashed;
            if (cntDel != 0)
            {
                var confirm = MessageBox.Show($"There {cntDel} folder marked for deletion. Do you really want them to delete?", "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Cancel)
                    cancel = true;

                if (confirm == MessageBoxResult.Yes)
                    delete=true;
            }
            var cntSave = PackView.FoldersSaved;
            if (cntSave != 0)
            {
                var confirm = MessageBox.Show($"There {cntSave} folders marked for save. Do you want them to copy to _Saved folder?", "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Cancel)
                    cancel=true;

                if (confirm == MessageBoxResult.Yes)
                {
                    save = true;
                    confirm = MessageBox.Show($"Do you want to remove original folders?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    deleteOriginal = confirm == MessageBoxResult.Yes;
                }
            }
            
            if (cancel)
            {
                e.Cancel = true;
                DImage.Source = source;
                return;
            }
            tokenSource.Cancel();
            while (foldersAsync.Status == TaskStatus.Running)
                Task.Delay(200);
            WindowState = WindowState.Minimized;
            PackView.Finalize(delete, save, deleteOriginal);
            ImageProcess.Close();
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void ThrashButton_Click(object sender, RoutedEventArgs e)
        {
            ThrashIt();
        }

        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveIt();
        }

        private void Fav_Click(object sender, RoutedEventArgs e)
        {
            AddToFav();
        }
        private void SaveIt()
        {
            if (PackView != null)
            {
                PackView.SaveIt();
                PackView.IsSaved = PackView.FolderIsSaved;
            }
        }

        private void AddToFav()
        {
            if (PackView != null)
            {
                PackView.AddToFav(filesInFolder[currentIndex]);
                PackView.IsFav = PackView.GetFavStatus(filesInFolder[currentIndex]);
            }
        }

        private void ThrashIt()
        {
            if (PackView != null)
            {
                PackView.ThrashIt();
                PackView.IsInThrash = PackView.FolderInThrash;
            }
        }
        private void FastForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackView != null && filesInFolder!=null)
            {
                currentIndex = Math.Min(currentIndex + FastForwardValue, filesInFolder.Count - 1);
                ShowImage();
            }
        }
        private void ToTheEndButton_Click(object sender, RoutedEventArgs e)
        {
            if (PackView != null && filesInFolder != null)
            {
                currentIndex = filesInFolder.Count - 1;
                ShowImage();
            }
        }
        private void FolderUpButton_Click(object sender, RoutedEventArgs e)
        {
            FolderUp();
        }
        private void FolderUp()
        {
            if (PackView != null)
            {
                PackView.FolderUp();
                GetFolderImages();
            }
        }
        private void FolderDownButton_Click(object sender, RoutedEventArgs e)
        {
            FolderDown();
        }
        private void FolderDown()
        {
            if (PackView != null)
            {
                PackView.FolderDown();
                GetFolderImages();
            }
        }
        private void GetFolders()
        {
            foldersAsync = Task.Factory.StartNew(() =>
            {
                try
                {
                    PackView.Init(_file, token);
                    PackView.BuildFolderList(token);
                    ShowStatus();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }, token);

            Dispatcher.Invoke(() =>
            {
                while (PackView==null || PackView.CanStartView == ReadyStatus.WaitingForFolderList)
                    Task.Delay(200);

                if (PackView.CanStartView == ReadyStatus.Failed)
                    Environment.Exit(0);

                GetFolderImages();
            });
        }
        private void GetFolderImages()
        {
            DImage.Source = null;
            var imagesAsync = Task.Factory.StartNew(() =>
            {
                try
                {
                    filesInFolder = PackView.GetCurrentFolderImages;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            });

            imagesAsync.ContinueWith(FolderImagesReceived);
        }
        private void FolderImagesReceived(Task obj)
        {
            Dispatcher.Invoke(() =>
            {
                currentIndex = 0;
                PackView.IsInThrash = PackView.FolderInThrash;
                PackView.IsSaved = PackView.FolderIsSaved;
                ShowImage();
            });
        }
        private void PrevImageButton_Click(object sender, RoutedEventArgs e)
        {
            PrevImage();
        }
        private void PrevImage()
        {
            if (PackView != null && filesInFolder != null)
            {
                if (currentIndex > 0)
                {
                    currentIndex--;
                    ShowImage();
                }
            }
        }
        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            NextImage();
        }
        private void NextImage()
        {
            if (PackView != null && filesInFolder != null)
            {
                if (currentIndex < filesInFolder.Count - 1)
                {
                    currentIndex++;
                    ShowImage();
                }
            }
        }
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                PrevImage();

            else if (e.Delta < 0)
                NextImage();
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                ImageProcess.Init(enableLibRaw);
                GetFolders();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                Environment.Exit(0);
            }
        }
    }
}
