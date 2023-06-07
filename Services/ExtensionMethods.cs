using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
