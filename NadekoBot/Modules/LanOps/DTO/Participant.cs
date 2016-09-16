using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.LanOps.DTO
{
    public class Participant
    {
        public int Id { set; get; }
        public string Seat { set; get; }
        public ParticipantUser User { set; get; }
    }
}
