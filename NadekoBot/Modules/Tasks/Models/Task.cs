using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.TaskItem.Models
{
    class TaskItem
    {
        public TaskItem()
        {
            Comments = new List<Comment>();
        }

        public DateTime? Due { set; get; }
        public string Text { set; get; }
        public List<Comment> Comments { set; get; }
        public User User { set; get; }
        public string Id { set; get; }
        public bool Complete { set; get; }
        public string List { set; get; }
    }
}
