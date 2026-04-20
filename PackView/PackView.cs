using ExifLib;
using Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PackViewer
{
    public enum ReadyStatus { WaitingForFolderList, FirstFolderReceived, Failed};

    public partial class ViewModel : ViewModelBase
    {
        private long _totalMemory, _cacheSize;
        //Queue<Action> _imageLoadingQueue;
        ImageQueue _imageLoadingQueue;
        public int StartImageIndex => _startImageIndex;
        public string CurrentFolderName => _currentFolder.FullPath;
        public int Enqueued => _imageLoadingQueue?.Count ?? 0;
        public int CurrentFolderIndex => _indexOfCurrentFolder;
        public int FoldersCount => _ft.Folders.Count;

        PackFolder _currentFolder;
        int _indexOfCurrentFolder;
        int _startImageIndex;
        private bool _allFoldersAreRead;
        public bool StartFolderIsSaved { get; private set; }
        public bool FolderInTrash  => _currentFolder.Status == Status.Delete; 
        public bool FolderIsSaved  => _currentFolder.Status == Status.Save; 
        public ReadyStatus CanStartView { get; private set; }

        public FileTask _ft;

        public Status GetFileStatus(string file)
        {
            var stat = Status.None;
            stat =_currentFolder.GetStatus(file);
            return stat;
        }

        public int FoldersThrashed => _ft.Folders.Count(r => r.Status == Status.Delete);
        public int FoldersSaved => _ft.Folders.Count(r => r.Status == Status.Save);

        Task _queueTask;
        string _startFile;

        public static bool IsRaw(string file) => file.ToLower().EndsWith("cr2") || file.ToLower().EndsWith("cr3") || file.ToLower().EndsWith("arw") || file.ToLower().EndsWith("rw2");
        public static bool IsJpeg(string file) => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".jpeg");

        public List<string> GetCurrentFolderImages
        {
            get
            {
                var nextImageFolder = new PackFolder();
                if (_indexOfCurrentFolder + 1 < _ft.Folders.Count)
                    nextImageFolder = _ft.Folders[_indexOfCurrentFolder + 1];
                
                // Always cache images in the first folder
                if (_indexOfCurrentFolder==0)
                    EnqueuLoadingFiles(_currentFolder, _ft.Folders[_indexOfCurrentFolder]);

                EnqueuLoadingFiles(_currentFolder, nextImageFolder);
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
            //if (!_allFoldersAreRead)
            //    return;

            if (!currentFolder.HasCache)
            {
                currentFolder.ImagesCache = new Dictionary<string, byte[]>();
                currentFolder.Cachesize = 0;
                if (!currentFolder.HasMetaCache)
                    currentFolder.MetaCache = new Dictionary<string, Meta>();
                foreach (var file in currentFolder.Files)
                    AddImage(file, currentFolder.FullPath);
            }
            if (nextImageFolder!=null && !nextImageFolder.HasCache)
            {
                nextImageFolder.ImagesCache = new Dictionary<string, byte[]>();
                nextImageFolder.Cachesize = 0;
                if (!nextImageFolder.HasMetaCache)
                    nextImageFolder.MetaCache = new Dictionary<string, Meta>();

                foreach (var file in nextImageFolder.Files)
                    AddImage(file, nextImageFolder.FullPath);
            }
        }

        public void FolderUp()
        {
            if (_indexOfCurrentFolder == 0) return;
            if (_indexOfCurrentFolder + 1 < _ft.Folders.Count)
            {
                RemoveCache(_ft.Folders[_indexOfCurrentFolder + 1]);
            }

            _indexOfCurrentFolder--;
            _currentFolder = _ft.Folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInTrash && AutoTrashFolder)
                _currentFolder.Status=Status.Delete;

            _ft.Update();
        }

        public void FolderDown()
        {
            if (_indexOfCurrentFolder + 1 >= _ft.Folders.Count)
                return;

            RemoveCache(_currentFolder);

            _indexOfCurrentFolder++;
            _currentFolder = _ft.Folders[_indexOfCurrentFolder];
            if (!FolderIsSaved && !FolderInTrash && AutoTrashFolder)
               _currentFolder.Status=Status.Delete;

            _ft.Update();
        }

        internal void TrashFolder()
        {
            if (_currentFolder.Status == Status.Delete)
                _currentFolder.Status = Status.None;
            else
                _currentFolder.Status = Status.Delete;
            
            IsFolderInTrash = FolderInTrash;
            _ft.Update();
        }
        internal void SaveFolder()
        {
            if (_currentFolder.Status == Status.Save)
                _currentFolder.Status = Status.None;
            else
                _currentFolder.Status = Status.Save;

            IsSaved = FolderIsSaved;

            _ft.Update();
        }

        public int ImageHeight(string v)
        {
            if (_currentFolder?.MetaCache != null && _currentFolder.MetaCache.TryGetValue(v, out Meta res))
                return res.Height;
            return 0;
        }

        public int ImageWidth(string v)
        {
            if (_currentFolder?.MetaCache != null && _currentFolder.MetaCache.TryGetValue(v, out Meta res))
                return res.Width;
            return 0;
        }

        internal void SetFileStatus(string v, Status stat)
        {
            if (!_currentFolder.ImagesStatus.ContainsKey(v))
                _currentFolder.ImagesStatus.Add(v, stat);
            else
            {
                // Cannot delete favorite
                if (_currentFolder.ImagesStatus[v] == Status.Save && stat == Status.Delete)
                    return;

                // Toggle status
                if (_currentFolder.ImagesStatus[v] == stat)
                    _currentFolder.ImagesStatus[v] = Status.None;
                else
                    _currentFolder.ImagesStatus[v] = stat;
            }

            IsFileDeleted = GetFileStatus(v) == Status.Delete;
            IsFileSaved = GetFileStatus(v) == Status.Save;

            _ft.Update();
        }

        internal void AddToAutoRemoveList(string v)
        {
            if (AutoRemoveFiles)
            {
                if (!_currentFolder.ImagesStatus.ContainsKey(v))
                    _currentFolder.ImagesStatus.Add(v, Status.AutoDelete);
                else
                {
                    if (_currentFolder.ImagesStatus[v] == Status.None) _currentFolder.ImagesStatus[v] = Status.AutoDelete;
                }
            }
            else
            {
                if (_currentFolder.ImagesStatus.ContainsKey(v) && _currentFolder.ImagesStatus[v] == Status.AutoDelete)
                {
                    _currentFolder.ImagesStatus[v] = Status.None;
                }
            }

            _ft.Update();
        }

        internal byte[] GetImage(string file, out Meta meta, out bool fromCache)
        {
            if (_currentFolder.HasCache && _currentFolder.ImagesCache.ContainsKey(file))
            {
                fromCache = true;
                meta= _currentFolder.MetaCache[file];
                return _currentFolder.ImagesCache[file];
            }
            else
            {
                using (Stream BitmapStream = System.IO.File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    fromCache = false;
                    byte[] array = new byte[new FileInfo(file).Length];
                    FileOps.ReadWholeArray(BitmapStream, array);
                    meta = new Meta();
                    if (IsJpeg(file))
                        meta.Rotation = GetMeta(array).Rotation;
                    else
                        meta.Rotation = Rotation.Rotate0;
                    return array;
                }
            }
        }
        
        private static Meta GetMeta(byte [] array)
        {
            var meta=new Meta();
            Rotation rot = Rotation.Rotate0;
            try
            {
                using (var reader = new ExifReader(array))
                {
                    // Get the image thumbnail (if present)
                    //var thumbnailBytes = reader.GetJpegThumbnailBytes();
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

            meta.Rotation = rot;
            return meta;
        }

        public void AddImage(string file, string key)
        {
            lock (_imageLoadingQueue)
            {
                if (_cacheSize > _totalMemory) 
                    return;

                var folder=_ft.Folders.FirstOrDefault(r => r.FullPath.Equals(key));

                ///
                // Why this condition is here?
                if (folder==null || folder.ImagesCache==null) 
                    return;

                _imageLoadingQueue.Enqueue(new ActionItem { Folder = folder, File = file, Action = ActionType.LoadImage });
            }
        }
        public void Init(string file, System.Threading.CancellationToken token)
        {
            var memInfo = GC.GetGCMemoryInfo();
            _totalMemory = memInfo.TotalAvailableMemoryBytes / 2;
            _cacheSize = 0;
            _startFile = file;
            _imageLoadingQueue = new ImageQueue();
            CanStartView = ReadyStatus.WaitingForFolderList;
            StartQueueTask(token);
        }

        private void StartQueueTask(System.Threading.CancellationToken token)
        {
            _queueTask = Task.Factory.StartNew(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    ActionItem action = null;
                    lock (_imageLoadingQueue)
                    {
                        StatusTop = _imageLoadingQueue.Count == 0 ? "" : $"Caching: {_imageLoadingQueue.Count}";
                        if (_imageLoadingQueue.Count > 0)
                            action = _imageLoadingQueue.Dequeue();
                    }

                    if (action == null)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    if (action.Action != ActionType.LoadImage) continue;

                    try
                    {
                        if (!action.Folder.ImagesCache.ContainsKey(action.File))
                        {
                            using (Stream bitmapStream = System.IO.File.Open(action.File,
                                       System.IO.FileMode.Open, FileAccess.Read))
                            {
                                byte[] array = new byte[new FileInfo(action.File).Length];
                                FileOps.ReadWholeArray(bitmapStream, array);
                                action.Folder.ImagesCache.Add(action.File, array);
                                action.Folder.Cachesize += array.Length;
                                _cacheSize += array.Length;
                                var meta = new Meta();
                                meta.Rotation = IsJpeg(action.File) ? GetMeta(array).Rotation : Rotation.Rotate0;
                                action.Folder.MetaCache.Add(action.File, meta);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
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

            if (_ft.GetCachedFile(dir, out List<string> f))
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
            _ft.Folders.Add(new PackFolder(dir, f));
        }

        public void BuildFolderList(System.Threading.CancellationToken token)
        {
            _currentFolder = new PackFolder();
            _indexOfCurrentFolder = -1;
            _startImageIndex = -1;
            _allFoldersAreRead = false;
            // Checking that an input is a file or a folder
            if (!File.Exists(_startFile) && !Directory.Exists(_startFile))
            {
                MessageBox.Show("Please open application by passing to it any image file in the folder", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CanStartView = ReadyStatus.Failed;
                return;
            }

            try
            {
                StatusBottom = "Building folder list";

                // Check if input is folder or file
                // In case _startFile is a file - use the directory where file is belonging as a starting folder
                // Read first main folder

                var imageFolder = Directory.Exists(_startFile) ? _startFile : Path.GetDirectoryName(_startFile);
                _ft.RootFolder = Path.GetFullPath(Path.Combine(imageFolder, @"..\"));

                StartFolderIsSaved = _ft.RootFolder.Contains(Global.FolderFavName);

                var dirs = CustomSearcher.GetDirectories(_ft.RootFolder, "*", SearchOption.TopDirectoryOnly).OrderBy(r=>r).ToList();
                if (token.IsCancellationRequested)
                    return;

                _ft.ReadCacheFile();
                int cnt = 0;
                AddFolders(token, imageFolder, imageFolder, ref cnt);
                if (!_ft.Folders.Any())
                {
                    MessageBox.Show($"No subdirectories are found in {_ft.RootFolder}", "Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    CanStartView = ReadyStatus.Failed;
                    return;
                }
                else
                {
                    _indexOfCurrentFolder = 0;
                    _currentFolder = _ft.Folders[0];
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
                _ft.Folders = _ft.Folders.OrderBy(r => r.FullPath).ToList();
                var currentPath = _currentFolder?.FullPath;
                if (currentPath != null)
                    _currentFolder = _ft.Folders.FirstOrDefault(r => r.FullPath.Equals(currentPath)) ?? _ft.Folders[0];
                else
                    _currentFolder = _ft.Folders[0];
                _indexOfCurrentFolder = _ft.Folders.IndexOf(_currentFolder);

                _allFoldersAreRead = true;
            }

            catch (Exception e)
            {
                //MessageBox.Show(e.Message);
                CanStartView = ReadyStatus.Failed;
            }
        }
    }
}
