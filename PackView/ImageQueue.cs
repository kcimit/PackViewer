using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackViewer
{
    public enum ActionType { LoadImage }
    public class ActionItem : IEquatable<ActionItem>
    {
        public PackFolder Folder { get; set; }
        public string File { get; set; }
        public ActionType Action { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as ActionItem);
        }

        public bool Equals(ActionItem other)
        {
            return other != null &&
                   Folder.FullPath == other.Folder.FullPath &&
                   File == other.File &&
                   Action == other.Action;
        }

        public override int GetHashCode()
        {
            int hashCode = 1806051143;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Folder.FullPath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(File);
            hashCode = hashCode * -1521134295 + Action.GetHashCode();
            return hashCode;
        }
    }
    public class ImageQueue
    {
        Queue<ActionItem> list = new Queue<ActionItem> ();
                                 //new LinkedList<ActionItem>();

        public void Enqueue(ActionItem t)
        {
            //list.AddLast(t);
            list.Enqueue (t);
        }

        public ActionItem Dequeue()
        {
            //var result = list.First.Value;
            //list.RemoveFirst();
            //return result;
            return list.Dequeue();
        }

        public ActionItem Peek()
        {
            return list.Peek();
        }

        public void Remove(ActionItem t)
        {
            list.Remove(t);
        }

        public int Count => list.Count; 

        public void StopLoading(PackFolder folder)
        {
            /*while(true)
            {
                var item=list.FirstOrDefault(r=>r.Folder == folder && r.Action== ActionType.LoadImage);
                if (item == null)
                    break;

                list.Remove(item);
            }*/
            var list2 = list.ToList(); //Needs to be copy, so we can clear the queue
            list.Clear();
            foreach (var item in list2)
            {
                if (item.Folder.FullPath == folder.FullPath)
                    continue;

                list.Enqueue(item);
            }
        }
    }
}
