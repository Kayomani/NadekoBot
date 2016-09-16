using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.LanOps
{
    public class WatchedLan
    {
        public ulong ServerId { set; get; }
        public ulong ChannelId { set; get; }
        public uint LanId { set; get; }
    }
}
