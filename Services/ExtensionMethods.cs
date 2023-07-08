using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PackViewer
{
    public static class ExtensionMethods
    {
        public static void Remove<T>(this Queue<T> queue, T itemToRemove) where T : class
        {
            var list = queue.ToList(); //Needs to be copy, so we can clear the queue
            queue.Clear();
            foreach (var item in list)
            {
                if (item == itemToRemove)
                    continue;

                queue.Enqueue(item);
            }
        }

        public static void RemoveAll<T>(this Queue<T> queue, List<T> itemToRemove) where T : class
        {
            var list = queue.ToList(); //Needs to be copy, so we can clear the queue
            queue.Clear();
            foreach (var item in list)
            {
                if (itemToRemove.Contains(item))
                    continue;

                queue.Enqueue(item);
            }
        }

        public static void SortPrioritizeDigits(this List<string> list)
        {
            if (list.Any() && list[0].Any(char.IsDigit))
            list.Sort(delegate(string a, string b)
            {
                a = new string(Path.GetFileName(a).Where(char.IsDigit).ToArray());
                b = new string(Path.GetFileName(b).Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
                if (string.IsNullOrEmpty(a)) return -1;
                if (string.IsNullOrEmpty(b)) return 1;

                return long.Parse(a).CompareTo(long.Parse(b));
            });
        }
    }
}
