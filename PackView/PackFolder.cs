using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PackViewer
{
    public class Meta
    {
        public Rotation Rotation { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Meta() { }
    }
    public enum Status { None, Delete, Save,
        AutoDelete
    };
    public class PackFolder
    {
        public PackFolder()
        {
            Status = Status.None;
            ImagesStatus = new Dictionary<string, Status>();
            Files=new List<string>();
            ImagesCache = new Dictionary<string, byte[]>();
            MetaCache = new Dictionary<string, Meta>();
        }

        public Dictionary<string, Status> ImagesStatus { get; set; }

        public PackFolder(string dir, List<string> _files)
        {
            Status = Status.None;
            ImagesStatus = new Dictionary<string, Status>();
            foreach (var file in _files) 
                ImagesStatus.Add(file, Status.None);
            Files = new List<string>();
            ImagesCache = new Dictionary<string, byte[]>();
            MetaCache = new Dictionary<string, Meta>();
            FullPath = dir;
            Files=new List<string>(_files);
            Files.SortPrioritizeDigits();
        }

        public List<string> Files {get;set;}
        public string RooFolder { get; set; }
        public string FullPath { get; set; }
        public Status Status { get; set; }
        public bool HasCache => ImagesCache!=null && ImagesCache.Keys.Any();
        public bool HasMetaCache => MetaCache != null && MetaCache.Keys.Any();
        public int Cachesize { get; internal set; }

        public Dictionary<string, byte[]> ImagesCache;
        public Dictionary<string, Meta> MetaCache;
        
        public Status GetStatus(string file)
        {
            var status = Status.None;
            if (ImagesStatus != null && ImagesStatus.ContainsKey(file))
                status = ImagesStatus[file];
            return status;
        }

        public long ClearCache()
        {
            ImagesCache=new Dictionary<string, byte[]>();
            return Cachesize;
        }
    }
}