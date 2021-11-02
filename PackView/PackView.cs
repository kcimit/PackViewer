using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PackViewer
{
    public enum ReadyStatus { WaitingForFolderList, FirstFolderReceived, Failed};
    public class CachedFiles
    {
        public Dictionary<string, List<string>> Files;
        public CachedFiles()
        { Files = new Dictionary<string, List<string>>(); }
    }
    public class PackView
    {
        Dictionary<string, Dictionary<string, byte[]>> _imagesCache;
        CachedFiles _cache;
        private long _totalMemory, _cacheSize;
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
        public bool StartFolderIs_Saved { get; private set; }
        public List<string> FoldersSaved => _foldersToSave;
        public bool FolderInThrash { get => _foldersToDelete.Contains(_currentFolder); }
        public bool FolderIsSaved { get => _foldersToSave.Contains(_currentFolder); }
        public ReadyStatus CanStartView { get; private set; }

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
                    RemoveCache(_folders[_indexOfCurrentFolder+1]);
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
                RemoveCache(_currentFolder);

            _indexOfCurrentFolder++;
            _currentFolder = _folders[_indexOfCurrentFolder];
        }
        private void RemoveCache(string folder)
        {
            foreach (var v in _imagesCache[folder].Values)
                _cacheSize -= v.Length;
            _imagesCache.Remove(folder);
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
                if (_cacheSize > _totalMemory) return;
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
                            _cacheSize+=array.Length;
                        }
                    }
                    catch { }
                });
            }
        }
        public PackView(string file, ViewModel vm, System.Threading.CancellationToken token)
        {
            using (Process proc = Process.GetCurrentProcess())
            {
                _totalMemory = proc.PrivateMemorySize64 / 2;
            }
            _cacheSize = 0;
            _foldersToDelete = new List<string>();
            _foldersToSave = new List<string>();
            _startFile = file;
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();
            _imageLoadingQueue = new Queue<Action>();
            _vm = vm;
            _cache = new CachedFiles();
            _queueCount = 0;
            CanStartView = ReadyStatus.WaitingForFolderList;
            StartQueueTask(token);
        }
        private void StartQueueTask(System.Threading.CancellationToken token)
        {
            _queueTask = Task.Factory.StartNew(() =>
              {
                  while (true && !token.IsCancellationRequested)
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

        private void AddFolders(System.Threading.CancellationToken token, string dir, string imageFolder, ref int curCount)
        {
            if (dir.Equals(imageFolder))
                return;

            if (!StartFolderIs_Saved && dir.Contains("_Saved"))
                return;

            if (_cache.Files.Any() && _cache.Files.TryGetValue(dir, out List<string> f))
                AddFolderAndFiles(dir, f);
            else
            {
                var files = FileOps.GetFiles(dir);
                if (files.Any())
                {
                    files.Sort();
                    AddFolderAndFiles(dir, files);
                }
            }
            if (token.IsCancellationRequested)
                return;

            var subFolders = CustomSearcher.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly).OrderBy(r=>r).ToList();
            foreach (var subFolder in subFolders)
            {
                if (token.IsCancellationRequested)
                    break;
                AddFolders(token, subFolder, imageFolder, ref curCount);
            }
            ++curCount;
        }

        private void AddFolderAndFiles(string dir, List<string> f)
        {
            _folders.Add(dir);
            _files.Add(dir, f);
        }

        public void BuildFolderList(System.Threading.CancellationToken token)
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
                CanStartView = ReadyStatus.Failed;
            }

            try
            {
                _vm.Status = "Building folder list";

                // Check if input is folder or file
                // In case _startFile is a file - use the directory where file is belonging as a starting folder
                // Read first main folder

                var imageFolder = Directory.Exists(_startFile) ? _startFile : Path.GetDirectoryName(_startFile);
                _rootFolder = Path.GetFullPath(Path.Combine(imageFolder, @"..\"));
                StartFolderIs_Saved=_rootFolder.Contains("_Saved");
                var dirs = CustomSearcher.GetDirectories(_rootFolder, "*", SearchOption.TopDirectoryOnly).OrderBy(r=>r).ToList();
                if (token.IsCancellationRequested)
                    return;

                ReadCacheFile();
                int cnt = 0;
                AddFolders(token, imageFolder, "", ref cnt);
                if (!_folders.Any())
                {
                    MessageBox.Show($"No subdirectories are found in {_rootFolder}", "Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    CanStartView = ReadyStatus.Failed;
                    return;
                }
                else
                {
                    _indexOfCurrentFolder = 0;
                    _currentFolder = _folders[0];
                    _startImageIndex = 0;
                    CanStartView = ReadyStatus.FirstFolderReceived;
                }

                foreach (var dir in dirs)
                {
                    if (token.IsCancellationRequested)
                        break;

                    AddFolders(token, dir, imageFolder, ref cnt);
                    _vm.Status = $"[{cnt}/{dirs.Count}]";
                    _folders.Sort();
                    _indexOfCurrentFolder = _folders.IndexOf(_currentFolder);
                }
                _allFoldersAreRead = true;
            }

            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                CanStartView = ReadyStatus.Failed;
            }
        }

        private void ReadCacheFile()
        {
            var fileCache = Path.Combine(_rootFolder, "cacheFolder.json");
            if (File.Exists(fileCache))
            {
                var r = File.ReadAllText(fileCache);
                _cache = JsonConvert.DeserializeObject<CachedFiles>(r);
            }
            if (_cache == null)
                _cache = new CachedFiles();
        }

        private void WriteCacheFile()
        {
            var c = new CachedFiles { Files = _files };

            var fileCache = Path.Combine(_rootFolder, "cacheFolder.json");
            using (var r = new StreamWriter(fileCache, false))
            {
                var json = JsonConvert.SerializeObject(c);
                r.Write(json);
            }
        }
        public void Finalize(bool delete, bool save, bool deleteOriginal)
        {
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();

            if (delete)
                FileOps.ProceedWithDeletion(_foldersToDelete, _vm);

            if (save)
                FileOps.ProceedWithSaving(_vm, _rootFolder, _foldersToSave, deleteOriginal);

            WriteCacheFile();
        }
    }
}
