using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PackViewer
{
    public static class FileOps
    {
        public static void ReadWholeArray(Stream stream, byte[] data)
        {
            int offset = 0;
            int remaining = data.Length;
            while (remaining > 0)
            {
                int read = stream.Read(data, offset, remaining);
                if (read <= 0)
                    throw new EndOfStreamException
                        ($"End of stream reached with {remaining} bytes left to read");
                remaining -= read;
                offset += read;
            }
        }
        public static List<string> GetFiles(string currentFolder)
        {
            string[] extensions = new[] { ".jpg", ".jpeg", ".cr2", ".cr3", ".arw" };

            FileInfo[] files =
                (new DirectoryInfo(currentFolder)).GetFiles("*.*", SearchOption.TopDirectoryOnly)
                     .Where(f => extensions.Contains(f.Extension.ToLower()))
                     .ToArray();

            return (files.Select(r => r.FullName)).ToList();
        }
        public static void ProceedWithSaving(ViewModel vm, bool deleteSource, string rootFolder, string subFolder, Status status, List<PackFolder> folders)
        {
            var destFolder = Path.Combine(rootFolder, subFolder);
            try
            {
                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            foreach (var folder in folders.Where(r => r.Status == status))
            {
                try
                {
                    var newFolder = Path.Combine(destFolder, folder.FullPath.Replace(rootFolder, ""));
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { vm.StatusBottom = $"Copying {folder.FullPath}"; }));
                    if (deleteSource)
                        MoveFilesRecursively(folder.FullPath, newFolder);
                    else
                        CopyFilesRecursively(folder.FullPath, newFolder);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }

        private static void MoveFilesRecursively(string sourcePath, string targetPath)
        {
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                File.Move(newPath, newPath.Replace(sourcePath, targetPath));
        }

        public static void ProceedWithDeletion(ViewModel vm, List<PackFolder> folders, Status status)
        {
            int skipped = 0;

            foreach (var folder in folders.Where(r => r.Status == status).OrderByDescending(r => r.FullPath.Length))
            {
                if (folder.ImagesStatus.Any(r=>r.Value== Status.Save))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (System.IO.Directory.GetDirectories(folder.FullPath).Length > 0)
                        skipped++;
                    else
                    {
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { vm.StatusBottom = $"Deleting {folder.FullPath}"; }));
                        DeleteFolder(folder.FullPath, folder.Files);
                    }

                    var folderUp = Path.GetFullPath(Path.Combine(folder.FullPath, @"..\"));
                    if (System.IO.Directory.GetDirectories(folderUp).Length == 0)
                    {
                        DeleteFolder(folderUp, null);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            if (skipped > 0)
                MessageBox.Show($"{skipped} directories were not removed since they contain subdirectories of favorite images.");
        }

        private static void DeleteFolder(string folder, List<string> files)
        {
            try
            {
                if (files != null)
                    foreach (string file in files)
                        File.Delete(file);

                if (!Directory.EnumerateFileSystemEntries(folder).Any())
                    Directory.Delete(folder, true);
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }

        internal static void ProceedWithFiles(ViewModel vm, List<PackFolder> folders)
        {
            foreach (var folder in folders.Where(r => r.Status != Status.None))
                foreach (var kvp in folder.ImagesStatus.Where(r=>r.Value!= Status.None))
                {
                    try
                    {
                        
                        var fld = Path.GetDirectoryName(kvp.Key);
                        if (fld == null) continue;
                        if (kvp.Value == Status.None)
                        {
                            fld = Path.Combine(fld, Global.FolderFavName);
                            if (!Directory.Exists(fld))
                                Directory.CreateDirectory(fld);

                            var newFile = Path.Combine(fld, Path.GetFileName(kvp.Key));
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { vm.StatusBottom = $"Copying {kvp.Key}"; }));
                            try
                            {
                                File.Copy(kvp.Key, newFile);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        }
                        else if(kvp.Value == Status.AutoDelete || kvp.Value== Status.Delete) 
                        {
                            fld = Path.Combine(fld, Global.FolderAutoRemoveName);
                            if (!Directory.Exists(fld))
                                Directory.CreateDirectory(fld);

                            var newFile = Path.Combine(fld, Path.GetFileName(kvp.Key));
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { vm.StatusBottom = $"Copying {kvp.Key}"; }));
                            try
                            {
                                File.Move(kvp.Key, newFile);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                            folder.Files.Remove(kvp.Key);
                        }
                        
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
        }
    }
}
