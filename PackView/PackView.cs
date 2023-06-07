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
        {
            Files = new Dictionary<string, List<string>>();
        }

        public CachedFiles(List<PackFolder> folders)
        { 
            Files = new Dictionary<string, List<string>>(); 
            foreach (var folder in folders.Where(r=>r.Status== Status.None))
                Files.Add(folder.FullPath, folder.Files);
        }
    }
    public partial class ViewModel : ViewModelBase
    {
        CachedFiles _cache;
        private long _totalMemory, _cacheSize;
        //Queue<Action> _imageLoadingQueue;
        ImageQueue _imageLoadingQueue;
        public int StartImageIndex => _startImageIndex;
        public string CurrentFolderName => _currentFolder.FullPath;
        public int Enqueued => _imageLoadingQueue.Count;
        public int CurrentFolderIndex => _indexOfCurrentFolder;
        public int FoldersCount => _folders.Count;

        PackFolder _currentFolder;
        private string _rootFolder;
        int _indexOfCurrentFolder;
        int _startImageIndex;
        private bool _allFoldersAreRead;
        List<PackFolder> _folders;
        public bool StartFolderIsSaved { get; private set; }
        public bool FolderInThrash { get => _currentFolder.Status == Status.Delete; }
        public bool FolderIsSaved { get => _currentFolder.Status == Status.Save; }
        public ReadyStatus CanStartView { get; private set; }
        public bool GetFavStatus(string file) => _currentFolder!=null && _currentFolder.GetFavStatus(file);
        public int FoldersThrashed => _folders.Count(r => r.Status == Status.Delete);
        public int FoldersSaved => _folders.Count(r => r.Status == Status.Save);

        Task _queueTask;
        string _startFile;
        
        public static bool IsRaw(string file) => file.ToLower().EndsWith("cr2") || file.ToLower().EndsWith("cr3") || file.ToLower().EndsWith("arw");
        public List<string> GetCurrentFolderImages
        {
            get
            {
                var _nextImageFolder = new PackFolder();
                if (_indexOfCurrentFolder + 1 < _folders.Count)
                    _nextImageFolder = _folders[_indexOfCurrentFolder + 1];
                EnqueuLoadingFiles(_currentFolder, _nextImageFolder);
                return _currentFolder.Files; 
            }
        }
        private void RemoveCache(PackFolder folder)
        {
            lock (_imageLoadingQueue)
            {
                _imageLoadingQueue.StopLoading(folder);
                _cacheSize -= folder.ClearCache();
            }
        }
        private void EnqueuLoadingFiles(PackFolder currentFolder, PackFolder nextImageFolder)
        {
            // Prevent caching, if still need to get contents of all the subfolders
            if (!_allFoldersAreRead)
                return;

            if (!currentFolder.HasCache)
            {
                currentFolder.ImagesCache = new Dictionary<string, byte[]>();
                currentFolder.Cachesize = 0;
                if (!currentFolder.HasRotCache)
                    currentFolder.RotCache = new Dictionary<string, Rotation>();
                foreach (var file in currentFolder.Files)
                    AddImage(file, currentFolder.FullPath);
            }
            if (nextImageFolder!=null && !nextImageFolder.HasCache)
            {
                nextImageFolder.ImagesCache = new Dictionary<string, byte[]>();
                nextImageFolder.Cachesize = 0;
                if (!nextImageFolder.HasRotCache)
                    nextImageFolder.RotCache = new Dictionary<string, Rotation>();

                foreach (var file in nextImageFolder.Files)
                    AddImage(file, nextImageFolder.FullPath);
            }
        }

        public void FolderUp()
        {
            if (_indexOfCurrentFolder == 0) return;
            if (_indexOfCurrentFolder + 1 < _folders.Count)
            {
                RemoveCache(_folders[_indexOfCurrentFolder + 1]);
            }

            _indexOfCurrentFolder--;
            _currentFolder = _folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInThrash && AutoTrashFolder)
                _currentFolder.Status=Status.Delete;
        }

        public void FolderDown()
        {
            if (_indexOfCurrentFolder + 1 >= _folders.Count)
                return;

            RemoveCache(_currentFolder);

            _indexOfCurrentFolder++;
            _currentFolder = _folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInThrash && AutoTrashFolder)
               _currentFolder.Status=Status.Delete;
        }

        internal void ThrashIt()
        {
            if (_currentFolder.Status == Status.Delete)
                _currentFolder.Status = Status.None;
            else
                _currentFolder.Status = Status.Delete;
        }
        internal void SaveIt()
        {
            if (_currentFolder.Status == Status.Save)
                _currentFolder.Status = Status.None;
            else
                _currentFolder.Status = Status.Save;
        }

        internal void AddToFav(string v)
        {
            if (_currentFolder.FavImages.Contains(v))
                _currentFolder.FavImages.Remove(v);
            else
                _currentFolder.FavImages.Add(v);
        }

        internal void AddToAutoRemoveList(string v)
        {
            if (!AutoRemoveFiles)
            {
                if (_currentFolder.AutoRemoveImages.Contains(v))
                    _currentFolder.AutoRemoveImages.Remove(v);
            }
            else
                if (!_currentFolder.AutoRemoveImages.Contains(v))
                    _currentFolder.AutoRemoveImages.Add(v);
        }

        internal byte[] GetImage(string file, out Rotation rot, out bool fromCache)
        {
            if (_currentFolder.HasCache && _currentFolder.ImagesCache.ContainsKey(file))
            {
                rot = _currentFolder.RotCache[file];
                fromCache = true;
                return _currentFolder.ImagesCache[file];
            }
            else
            {
                using (Stream BitmapStream = System.IO.File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    fromCache = false;
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
                if (_cacheSize > _totalMemory) 
                    return;

                var folder=_folders.Where(r=>r.FullPath.Equals(key)).FirstOrDefault();

                ///
                // Why this condition is here?
                if (folder==null || folder.ImagesCache==null) 
                    return;

                _imageLoadingQueue.Enqueue(new ActionItem { Folder = folder, File = file, Action = ActionType.LoadImage });
            }
        }
        public void Init(string file, System.Threading.CancellationToken token)
        {
            using (Process proc = Process.GetCurrentProcess())
            {
                _totalMemory = proc.PrivateMemorySize64;
            }
            _cacheSize = 0;
            _startFile = file;
            _imageLoadingQueue = new ImageQueue();
            _cache = new CachedFiles();
            CanStartView = ReadyStatus.WaitingForFolderList;
            StartQueueTask(token);
        }

        private void StartQueueTask(System.Threading.CancellationToken token)
        {
            _queueTask = Task.Factory.StartNew(() =>
              {
                  while (true && !token.IsCancellationRequested)
                  {
                      StatusTop = Enqueued == 0 ? "" : $"Caching: {Enqueued}";

                      lock (_imageLoadingQueue)
                      {
                          if (_imageLoadingQueue.Count > 0)
                          {
                              var action = _imageLoadingQueue.Dequeue();
                              if (action != null && action.Action == ActionType.LoadImage)
                              {
                                  try
                                  {
                                      if (!action.Folder.ImagesCache.ContainsKey(action.File))
                                      {
                                          using (Stream BitmapStream = System.IO.File.Open(action.File, System.IO.FileMode.Open, FileAccess.Read))
                                          {
                                              byte[] array = new byte[new FileInfo(action.File).Length];
                                              FileOps.ReadWholeArray(BitmapStream, array);
                                              action.Folder.ImagesCache.Add(action.File, array);
                                              action.Folder.Cachesize += array.Length;
                                              _cacheSize += array.Length;
                                              action.Folder.RotCache.Add(action.File, GetOrientation(array));
                                          }
                                      }
                                  }
                                  catch { }
                              }
                          }
                      }
                  }
              });
        }

        private void AddFolders(System.Threading.CancellationToken token, string dir, string imageFolder, ref int curCount)
        {
            if (dir.Equals(imageFolder))
                return;

            if (!StartFolderIsSaved && (dir.Contains(Global.FolderDeletedName) || dir.Contains(Global.FolderSavedName) || dir.Contains(Global.FolderAutoRemoveName) || dir.Contains(Global.FolderFavName)))
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
            _folders.Add(new PackFolder(dir, f));
        }

        public void BuildFolderList(System.Threading.CancellationToken token)
        {
            _currentFolder = new PackFolder();
            _indexOfCurrentFolder = -1;
            _folders = new List<PackFolder>();
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

                StartFolderIsSaved = _rootFolder.Contains(Global.FolderFavName);

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
                    
                }
                _folders = _folders.OrderBy(r => r.FullPath).ToList();
                _currentFolder = _folders.Where(r => r.FullPath.Equals(_currentFolder.FullPath)).FirstOrDefault();
                _indexOfCurrentFolder = _folders.IndexOf(_currentFolder);

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
            if (_folders.Count < Global.MinNumberOfFoldersToCache)
                return;

            if (!Directory.Exists(_rootFolder))
                return;

            var c = new CachedFiles (_folders);
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
            FileOps.ProceedWithCopyingFav(this, _folders);
            FileOps.ProceedWithAutoMoving(this, _folders);

            if (save)
            {
                FileOps.ProceedWithSaving(this, deleteOriginal, _rootFolder, Global.FolderSavedName, Status.Save, _folders);
                if (deleteOriginal)
                   FileOps.ProceedWithDeletion(this, _folders, Status.Save);
            }

            if (delete)
            {
                if (Global.BackupDeleted)
                {
                    FileOps.ProceedWithSaving(this, true, _rootFolder, Global.FolderDeletedName, Status.Delete, _folders);
                    FileOps.ProceedWithDeletion(this, _folders, Status.Delete);
                }
                else
                    FileOps.ProceedWithDeletion(this, _folders, Status.Delete);
            }
            WriteCacheFile();
        }
    }
}
