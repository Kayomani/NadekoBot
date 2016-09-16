using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.LanOps.DTO
{
    public class ParticipantUser
    {
        public int Id { set; get; }
        public string SteamName { set; get; }
        public string Firstname { set; get; }
        public string Surname { set; get; }
    }
}
