using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace PackViewer
{
    public enum Status { None, Delete, Save};
    public class PackFolder
    {
        public PackFolder()
        {
            Status = Status.None;
            FavImages = new List<string>();
            Files=new List<string>();
            ImagesCache = new Dictionary<string, byte[]>();
            RotCache = new Dictionary<string, Rotation>();
        }

        public PackFolder(string dir, List<string> _files)
        {
            Status = Status.None;
            FavImages = new List<string>();
            Files = new List<string>();
            ImagesCache = new Dictionary<string, byte[]>();
            RotCache = new Dictionary<string, Rotation>();
            FullPath = dir;
            Files=new List<string>(_files);
        }

        public List<string> Files {get;set;}
        public string RooFolder { get; set; }
        public string FullPath { get; set; }
        public Status Status { get; set; }
        public List<string> FavImages { get; set; }
        public bool HasCache => ImagesCache!=null && ImagesCache.Keys.Any();
        public bool HasRotCache => RotCache != null && RotCache.Keys.Any();

        public Dictionary<string, byte[]> ImagesCache;
        public Dictionary<string, Rotation> RotCache;

        public bool GetFavStatus(string file) => FavImages != null && FavImages.Contains(file);

        public long ClearCache()
        {
            var size = ImagesCache.Values.Sum(r => r.Length);
            ImagesCache=new Dictionary<string, byte[]>();
            return size;
        }
    }
}