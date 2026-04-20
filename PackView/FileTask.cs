using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PackViewer
{
    public class FileTask
    {
        List<PackFolder> _folders;
        private bool _enableCachingFolderContent;
        CachedFiles _cache;
        private string _tempFile;
        Stopwatch _stopwatch;

        public FileTask(bool enableCachingFolderContent)
        {
            _folders = new List<PackFolder>();
            _cache = new CachedFiles();
            _enableCachingFolderContent = enableCachingFolderContent;

            _tempFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PackView.tmp");
            if (File.Exists(_tempFile))
            {
                try
                {
                    var r = File.ReadAllText(_tempFile);
                    _folders = JsonConvert.DeserializeObject<List<PackFolder>>(r) ?? new List<PackFolder>();
                    var res = MessageBox.Show(
                        "Unprocessed task exist from unfinished session. Do you want to process the task?",
                        "Question",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        Finalize(null, true, true, true);
                        _folders = new List<PackFolder>();
                        File.Delete(_tempFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }


        public List<PackFolder> Folders
        {
            get => _folders;
            set => _folders = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string RootFolder { get; set; }

        public bool GetCachedFile(string f, out List<string> cached)
        {
            cached = new List<string>();
            return _cache.Files.Any() && _cache.Files.TryGetValue(f, out cached);
        }

        public void Finalize(ViewModel vm, bool delete, bool save, bool deleteOriginal)
        {
            FileOps.ProceedWithFiles(vm, _folders);

            if (save)
            {
                FileOps.ProceedWithSaving(vm, deleteOriginal, RootFolder, Global.FolderSavedName, Status.Save,
                    _folders);
                if (deleteOriginal)
                    FileOps.ProceedWithDeletion(vm, _folders, Status.Save);
            }

            if (delete)
            {
                if (Global.BackupDeleted)
                {
                    FileOps.ProceedWithSaving(vm, true, RootFolder, Global.FolderDeletedName, Status.Delete, _folders);
                    FileOps.ProceedWithDeletion(vm, _folders, Status.Delete);
                }
                else
                    FileOps.ProceedWithDeletion(vm, _folders, Status.Delete);
            }

            if (_enableCachingFolderContent)
                WriteCacheFile();

            File.Delete(_tempFile);
        }

        private void WriteCacheFile()
        {
            if (_folders.Count < Global.MinNumberOfFoldersToCache)
                return;

            if (!Directory.Exists(RootFolder))
                return;

            var c = new CachedFiles(_folders);
            try
            {
                var fileCache = Path.Combine(RootFolder, "cacheFolder.json");
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



        public void ReadCacheFile()
        {
            if (_enableCachingFolderContent)
            {
                try
                {
                    var fileCache = Path.Combine(RootFolder, "cacheFolder.json");
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
            }
            else
            {
                try
                {
                    var fileCache = Path.Combine(RootFolder, "cacheFolder.json");
                    if (File.Exists(fileCache))
                        File.Delete(fileCache);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            _cache ??= new CachedFiles();
        }

        public void Update()
        {
            if (_stopwatch.Elapsed.TotalSeconds > 60)
                try
                {
                    using (var r = new StreamWriter(_tempFile, false))
                    {
                        var json = JsonConvert.SerializeObject(_folders);
                        r.Write(json);
                    }

                    _stopwatch.Restart();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    _stopwatch.Restart();
                }
        }
    }

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
            foreach (var folder in folders.Where(r => r.Status == Status.None))
                Files.Add(folder.FullPath, folder.Files);
        }
    }
}
