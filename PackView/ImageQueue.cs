using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackViewer
{
    public enum ActionType { LoadImage,
        Delete
    }
    public class ActionItem
    {
        public PackFolder Folder { get; set; }
        public string File { get; set; }
        public ActionType Action { get; set; }
    }
    public class ImageQueue
    {
        LinkedList<ActionItem> list = new LinkedList<ActionItem>();

        public void Enqueue(ActionItem t)
        {
            list.AddLast(t);
        }

        public ActionItem Dequeue()
        {
            var result = list.First.Value;
            list.RemoveFirst();
            return result;
        }

        public ActionItem Peek()
        {
            return list.First.Value;
        }

        public bool Remove(ActionItem t)
        {
            return list.Remove(t);
        }

        public int Count { get => list.Count;  }

        public void StopLoading(PackFolder folder)
        {
            while(true)
            {
                var item=list.FirstOrDefault(r=>r.Folder == folder && r.Action== ActionType.LoadImage);
                if (item == null)
                    break;

                list.Remove(item);
            }
        }
    }
}
