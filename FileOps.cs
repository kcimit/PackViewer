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
    public static class FileOps
    {
        public static List<string> GetFiles(string currentFolder)
        {
            string[] extensions = new[] { ".jpg", ".jpeg", ".cr2", ".cr3", ".arw" };

            FileInfo[] files =
                (new DirectoryInfo(currentFolder)).GetFiles("*.*", SearchOption.TopDirectoryOnly)
                     .Where(f => extensions.Contains(f.Extension.ToLower()))
                     .ToArray();

            return (files.Select(r => r.FullName)).ToList();
        }
        public static void ProceedWithSaving(ViewModel vm, string rootFolder, List<string> foldersToSave, bool deleteOriginal)
        {
            var destFolder = Path.Combine(rootFolder, "_Saved");
            try
            {
                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            foreach (var folder in foldersToSave)
            {
                try
                {
                    var newFolder = Path.Combine(destFolder, folder.Replace(rootFolder, ""));
                    CopyFilesRecursively(folder, newFolder);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            if (deleteOriginal)
                ProceedWithDeletion(foldersToSave, vm);
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);

            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }
        public static void ProceedWithDeletion(List<string> foldersToDelete, ViewModel vm)
        {
            int skipped = 0;

            foreach (var folder in foldersToDelete.OrderByDescending(r => r.Length))
            {
                try
                {
                    if (System.IO.Directory.GetDirectories(folder).Length > 0)
                        skipped++;
                    else
                    {
                        Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() => { vm.Status = $"Deleting {folder}"; }));
                        Directory.Delete(folder, true);
                    }

                    var folderUp = Path.GetFullPath(Path.Combine(folder, @"..\"));
                    if (System.IO.Directory.GetDirectories(folderUp).Length == 0)
                    {
                        Directory.Delete(folderUp, true);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            if (skipped > 0)
                MessageBox.Show($"{skipped} directories were not removed since they contain subdirectories.");
        }
    }
}
