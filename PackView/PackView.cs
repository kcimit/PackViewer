using ExifLib;
using Newtonsoft.Json;
using Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
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
    public partial class ViewModel : ViewModelBase
    {
        Dictionary<string, Dictionary<string, byte[]>> _imagesCache;
        Dictionary<string, Dictionary<string, Rotation>> _rotCache;
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
        List<string> _favImages;

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
        private int _queueCount;
        public static bool IsRaw(string file) => file.ToLower().EndsWith("cr2") || file.ToLower().EndsWith("cr3") || file.ToLower().EndsWith("arw");
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

        
        private void RemoveCache(string folder)
        {
            _cacheSize -= _imagesCache[folder].Values.Sum(r => r.Length);
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
                if (!_rotCache.ContainsKey(currentFolder))
                    _rotCache.Add(currentFolder, new Dictionary<string, Rotation>());
                foreach (var file in _files[currentFolder])
                {
                    AddImage(file, currentFolder);
                }
            }
            if (!string.IsNullOrEmpty(nextImageFolder) && !_imagesCache.ContainsKey(nextImageFolder))
            {
                _imagesCache.Add(nextImageFolder, new Dictionary<string, byte[]>());
                if (!_rotCache.ContainsKey(nextImageFolder))
                    _rotCache.Add(nextImageFolder, new Dictionary<string, Rotation>());

                foreach (var file in _files[nextImageFolder])
                {
                    AddImage(file, nextImageFolder);
                }
            }
        }

        internal bool GetFavStatus(string file) => _favImages != null && _favImages.Contains(file);

        public void FolderUp()
        {
            if (_indexOfCurrentFolder == 0) return;
            if (_indexOfCurrentFolder + 1 < _folders.Count && _imagesCache.ContainsKey(_folders[_indexOfCurrentFolder + 1]))
                RemoveCache(_folders[_indexOfCurrentFolder + 1]);

            _indexOfCurrentFolder--;
            _currentFolder = _folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInThrash && AutoTrashFolder)
                _foldersToDelete.Add(_currentFolder);
        }

        public void FolderDown()
        {
            if (_indexOfCurrentFolder + 1 >= _folders.Count)
                return;
            if (_imagesCache.ContainsKey(_currentFolder))
                RemoveCache(_currentFolder);

            _indexOfCurrentFolder++;
            _currentFolder = _folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInThrash && AutoTrashFolder)
                _foldersToDelete.Add(_currentFolder);
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

        internal void AddToFav(string v)
        {
            if (_favImages.Contains(v))
                _favImages.Remove(v);
            else
                _favImages.Add(v);
        }

        internal byte[] GetImage(string file, out Rotation rot)
        {
            if (_imagesCache.ContainsKey(_currentFolder) && _imagesCache[_currentFolder].ContainsKey(file))
            {
                rot = _rotCache[_currentFolder][file];
                return _imagesCache[_currentFolder][file];
            }
            else
            {
                using (Stream BitmapStream = System.IO.File.Open(file, System.IO.FileMode.Open))
                {
                    byte[] array = new byte[new FileInfo(file).Length];
                    FileOps.ReadWholeArray(BitmapStream, array);

                    rot = GetOrientation(array);
                    return array;
                }
            }
        }

        private static Rotation GetOrientation(byte [] array)
        {
            Rotation rot = Rotation.Rotate0;
            try
            {
                using (var reader = new ExifReader(array))
                {
                    // Get the image thumbnail (if present)
                    var thumbnailBytes = reader.GetJpegThumbnailBytes();
                    reader.GetTagValue(ExifTags.Orientation, out ushort orientation);

                    switch (orientation)
                    {
                        case 3:
                        case 4:
                            rot = Rotation.Rotate180;
                            break;
                        case 5:
                        case 6:
                            rot = Rotation.Rotate90;
                            break;
                        case 7:
                        case 8:
                            rot = Rotation.Rotate270;
                            break;
                    }
                }
            }
            catch { }
            return rot;
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
                        if (_imagesCache[key].ContainsKey(file)) return;
                        
                        using (Stream BitmapStream = System.IO.File.Open(file, System.IO.FileMode.Open))
                        {
                            byte[] array = new byte[new FileInfo(file).Length];
                            FileOps.ReadWholeArray(BitmapStream, array);
                            _imagesCache[key].Add(file, array);
                            _cacheSize += array.Length;
                            _rotCache[key].Add(file, GetOrientation(array));
                        }
                        
                    }
                    catch { }
                });
            }
        }
        public void Init(string file, System.Threading.CancellationToken token)
        {
            using (Process proc = Process.GetCurrentProcess())
            {
                _totalMemory = proc.PrivateMemorySize64 / 2;
            }
            _cacheSize = 0;
            _foldersToDelete = new List<string>();
            _foldersToSave = new List<string>();
            _favImages = new List<string>();
            _startFile = file;
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();
            _rotCache = new Dictionary<string, Dictionary<string, Rotation>>();
            _imageLoadingQueue = new Queue<Action>();
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
                            StatusTop = $"{_queueCount}";
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

            if (!StartFolderIs_Saved && (dir.Contains("_Saved") || dir.Contains("_FavPackViewer")))
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
                StatusBottom = "Building folder list";

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
                    StatusBottom = $"[{cnt}/{dirs.Count}]";
                    _folders.Sort();
                    _indexOfCurrentFolder = _folders.IndexOf(_currentFolder);
                }
                _allFoldersAreRead = true;
            }

            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                CanStartView = ReadyStatus.Failed;
            }
        }

        private void ReadCacheFile()
        {
            try
            {
                var fileCache = Path.Combine(_rootFolder, "cacheFolder.json");
                if (File.Exists(fileCache))
                {
                    var r = File.ReadAllText(fileCache);
                    _cache = JsonConvert.DeserializeObject<CachedFiles>(r);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            if (_cache == null)
                _cache = new CachedFiles();
        }

        private void WriteCacheFile()
        {
            if (!Directory.Exists(_rootFolder))
                return;

            var c = new CachedFiles { Files = _files };
            try
            {
                var fileCache = Path.Combine(_rootFolder, "cacheFolder.json");
                using (var r = new StreamWriter(fileCache, false))
                {
                    var json = JsonConvert.SerializeObject(c);
                    r.Write(json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        public void Finalize(bool delete, bool save, bool deleteOriginal)
        {
            _imagesCache = new Dictionary<string, Dictionary<string, byte[]>>();
            _rotCache = new Dictionary<string, Dictionary<string, Rotation>>();

            if (delete)
                FileOps.ProceedWithDeletion(_foldersToDelete, this);

            if (save)
                FileOps.ProceedWithSaving(this, _rootFolder, _foldersToSave, deleteOriginal);

            if (_favImages != null && _favImages.Any())
                FileOps.ProceedWithCopyingFav(this, _favImages);

            WriteCacheFile();
        }
    }
}
