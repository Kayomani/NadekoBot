using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.TaskItem.Models
{
    class MonitoredLists
    {
        public ulong ServerId { set; get; }
        public ulong ChannelId { set; get; }
        public string List { set; get; }
    }
}
