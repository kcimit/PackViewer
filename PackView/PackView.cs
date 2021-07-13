using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PackViewer
{
    public class PackView
    {
        Dictionary<string, Dictionary<string, byte[]>> _imagesCache;
        Queue<Action> _imageLoadingQueue;
        public int StartImageIndex => _startImageIndex;
        public string GetCurrentFolderName => _currentFolder;
        public int Enqueued => _imageLoadingQueue.Count;
        public int CurrentFolderIndex => _indexOfCurrentFolder;
        public int FoldersCount => _folders.Count;

        string _currentFolder;
        private string _rootFolder;
        int _indexOfCurrentFolder;
        int _startImageIndex;
        private bool _allFoldersAreRead;
        List<string> _folders;

        Dictionary<string, List<string>> _files;
        List<string> _foldersToDelete { get; set; }
        List<string> _foldersToSave { get; set; }
        public List<string> FoldersTrashed => _foldersToDelete;
        public List<string> FoldersSaved => _foldersToSave;
        public bool FolderInThrash { get => _foldersToDelete.Contains(_currentFolder); }
        public bool FolderIsSaved { get => _foldersToSave.Contains(_currentFolder); }
        public bool CanStartView { get; private set; }

        Task _queueTask;
        string _startFile;
        ViewModel _vm;
        private int _queueCount;
        public List<string> GetCurrentFolderImages
        {
            get
            {
                var _nextImageFolder = string.Empty;
                if (_indexOfCurrentFolder + 1 < _folders.Count)
                    _nextImageFolder = _folders[_indexOfCurrentFolder + 1];
                EnqueuLoadingFiles(_currentFolder, _nextImageFolder);
                return _files[_currentFolder]; 
            }
        }

        public void FolderUp()
        {
            if (_indexOfCurrentFolder == 0) return;
            if (_indexOfCurrentFolder + 1 < _folders.Count)
            {
                if (_imagesCache.ContainsKey(_folders[_indexOfCurrentFolder+1]))
                    _imagesCache.Remove(_folders[_indexOfCurrentFolder+1]);
            }
            _indexOfCurrentFolder--;
            _currentFolder = _folders[_indexOfCurrentFolder];
        }

        internal static bool IsRaw(string file) => file.ToLower().EndsWith("cr2") || file.ToLower().EndsWith("cr3") || file.ToLower().EndsWith("arw");

        public void FolderDown()
        {
            if (_indexOfCurrentFolder + 1 >= _folders.Count)
                return;
            if (_imagesCache.ContainsKey(_currentFolder))
                _imagesCache.Remove(_currentFolder);

            _indexOfCurrentFolder++;
            _currentFolder = _folders[_indexOfCurrentFolder];
        }
        private void EnqueuLoadingFiles(string currentFolder, string nextImageFolder)
        {
            // Prevent caching, if still need to get contents of all the subfolders
            if (!_allFoldersAreRead)
                return;

            if (!_imagesCache.ContainsKey(currentFolder))
            {
                _imagesCache.Add(currentFolder, new Dictionary<string, byte[]>());
                foreach (var file in _files[currentFolder])
                {
                    AddImage(file, currentFolder);
                }
            }
            if (!string.IsNullOrEmpty(nextImageFolder) && !_imagesCache.ContainsKey(nextImageFolder))
            {
                _imagesCache.Add(nextImageFolder, new Dictionary<string, byte[]>());
                foreach (var file in _files[nextImageFolder])
                {
                    AddImage(file, nextImageFolder);
                }
            }
        }
        
        internal void ThrashIt()
        {
            if (_foldersToDelete.Contains(_currentFolder))
                _foldersToDelete.Remove(_currentFolder);
            else
                _foldersToDelete.Add(_currentFolder);
        }
        internal void SaveIt()
        {
            if (_foldersToSave.Contains(_currentFolder))
                _foldersToSave.Remove(_currentFolder);
            else
                _foldersToSave.Add(_currentFolder);
        }
        internal byte[] GetImage(string file)
        {
            if (_imagesCache.ContainsKey(_currentFolder) && _imagesCache[_currentFolder].ContainsKey(file))
                return _imagesCache[_currentFolder][file];
            else
            {
                using (Stream BitmapStream = System.IO.File.Open(file, System.IO.FileMode.Open))
                {
                    byte[] array = new byte[new FileInfo(file).Length];
                    FileOps.ReadWholeArray(BitmapStream, array);
                    return array;
                }
            }
        }
        public void AddImage(string file, string key)
        {
            lock (_imageLoadingQueue)
            {
                if (!_imagesCache.ContainsKey(key)) return;
                _imageLoadingQueue.Enqueue(() =>
                {
                    try
                    {
                        if (!_imagesCache.ContainsKey(key)) return;
                        using (Stream BitmapStream = System.IO.File.Open(file, System.IO.FileMode.Open))
                        {
                            byte[] array = new byte[new FileInfo(file).Length];
                            FileOps.ReadWholeArray(BitmapStream, array);
                            _imagesCache[key].Add(file, array);
                        }
                    }
                    catch { }
                });
            }
        }
        public PackView(string file, ViewModel vm)
        {
            _foldersToDelete = new List<string>();
            _foldersToSave = new List<string>();
            _startFile = file;
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();
            _imageLoadingQueue = new Queue<Action>();
            _vm = vm;
            _queueCount = 0;
            StartQueueTask();
            CanStartView=BuildFolderList();
        }
        private void StartQueueTask()
        {
            _queueTask = Task.Factory.StartNew(() =>
              {
                  while (true)
                  {
                      if (_queueCount!=Enqueued)
                      {
                          _queueCount = Enqueued;
                          if (_vm!=null)
                            _vm.Status2 = $"{_queueCount}";
                      }
                      lock (_imageLoadingQueue)
                      {
                          if (_imageLoadingQueue.Count > 0)
                              _imageLoadingQueue.Dequeue().Invoke();
                      }
                  }
              });
        }

        private void AddFolders(string dir, string imageFolder, int count, ref int curCount)
        {
            if (dir.Equals(imageFolder) || dir.Contains("_Saved"))
                return;

            var files = FileOps.GetFiles(dir);
            if (files.Any())
            {
                _folders.Add(dir);
                files.Sort();
                _files.Add(dir, files);
            }
            var subFolders = CustomSearcher.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly);
            foreach (var subFolder in subFolders)
                AddFolders(subFolder, imageFolder, count, ref curCount);

            _vm.Status = $"[{++curCount}/{count}]";
        }

        private bool BuildFolderList()
        {
            _currentFolder = string.Empty;
            _indexOfCurrentFolder = -1;
            _folders = new List<string>();
            _files = new Dictionary<string, List<string>>();
            _startImageIndex = -1;
            _allFoldersAreRead = false;
            // Checking that an input is a file or a folder
            if (!File.Exists(_startFile) && !Directory.Exists(_startFile))
            {
                MessageBox.Show("Please open application by passing to it any image file in the folder", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            
            try
            {
                _vm.Status = "Building folder list";
                // Check if input is folder or file
                // In case _startFile is a file - use the directory where file is belonging as a starting folder
                var imageFolder = Directory.Exists(_startFile)? _startFile : Path.GetDirectoryName(_startFile);
                _rootFolder = Path.GetFullPath(Path.Combine(imageFolder, @"..\"));
                int cnt = 0;
                AddFolders(imageFolder, "", 0, ref cnt);
                if (!_folders.Any())
                {
                    MessageBox.Show($"No subdirectories are found in {_rootFolder}", "Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return false;
                }

                // Read first main folder
                _indexOfCurrentFolder = 0;
                _currentFolder = _folders[0];
                _startImageIndex = 0;

                var dirs = CustomSearcher.GetDirectories(_rootFolder, "*", SearchOption.TopDirectoryOnly);

                // Then create a task to build the rest in the background
                var task = Task.Factory.StartNew(() =>
                {
                    foreach (var dir in dirs)
                    {
                        AddFolders(dir, imageFolder, dirs.Count, ref cnt);
                    }
                    _currentFolder = _folders[_indexOfCurrentFolder];
                    _allFoldersAreRead = true;
                });
            }

            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return false;
            }
            return true;
        }
        public void Finalize(bool delete, bool save, bool deleteOriginal)
        {
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();

            if (delete)
                FileOps.ProceedWithDeletion(_foldersToDelete, _vm);

            if (save)
                FileOps.ProceedWithSaving(_vm, _rootFolder, _foldersToSave, deleteOriginal);
        }
    }
}
