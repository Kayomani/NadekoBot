using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Modules;
using Discord.Commands;
using NadekoBot.Classes;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using NadekoBot.Modules.LanOps.DTO;

namespace NadekoBot.Modules.LanOps
{
    class LanOpsModule : DiscordModule
    {
        public override string Prefix { get; } = NadekoBot.Config.CommandPrefixes.LanOps;

        private List<WatchedLan> watchedLans = new List<WatchedLan>();
        private const string configPath = "data/lanops.json";

        private void LoadConfig()
        {
            if(File.Exists(configPath))
            {
                var savedConfig = JsonConvert.DeserializeObject<SavedConfig>(File.ReadAllText(configPath));
                watchedLans = savedConfig.WatchedLans;
            }
        }

        private void SaveConfig()
        {
            var configObject = new SavedConfig() { WatchedLans = watchedLans.ToList() };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(configObject));
        }

        public override void Install(ModuleManager manager)
        {
            LoadConfig();

            manager.CreateCommands("", cgb =>
            {
                cgb.CreateCommand(Prefix + "readme")
                .Alias(Prefix + "guide")
                 .Description($"Show command info")
                 .Parameter("lan", ParameterType.Unparsed)
                 .Do(async e =>
                 {
                     await e.Channel.SendMessage("See <http://nadekobot.readthedocs.io/en/latest/Commands%20List/>");
                 });

                cgb.CreateCommand(Prefix + "attendees")
                   .Description($"Shows the attendees at the next lan")
                   .Parameter("lan", ParameterType.Unparsed)
                   .Do(async e =>
                   {
                       var tag = e.GetArg("lan")?.Trim() ?? "";
                       if (tag == "")
                           tag = "25";

                       uint lanNumber = 0;
                       if (!uint.TryParse(tag, out lanNumber))
                       {
                           await e.Channel.SendMessage("`Failed to parse LAN number`");
                           return;
                       }

                       if(lanNumber<25)
                       {
                           await e.Channel.SendMessage("`Sorry pal, dont have that. Lan 25+ only. Blame the fact we have cool new shit`");
                           return;
                       }

                       try
                       {
                           var users = await GetAttendeeList(lanNumber);

                           string msg = $"Attendee list for LanOps {lanNumber}\r\n";
                           for (int i = 0; i < users.Count(); i++)
                           {
                               if(string.IsNullOrWhiteSpace(users.ElementAt(i).Seat) || string.IsNullOrWhiteSpace(users.ElementAt(i).Seat))
                                  msg += $"\r\n{(i+1)}. {users.ElementAt(i).User.SteamName }";
                               else
                                  msg += $"\r\n{(i + 1)}. {users.ElementAt(i).User.SteamName } Seat: {users.ElementAt(i).Seat}";
                           }

                           await e.Channel.SendMessage($"`{msg}`".Replace("@","\\@"));
                       }
                       catch
                       {
                           await e.Channel.SendMessage("`Failed to get the attendee list :<`");
                       }
                   });


                cgb.CreateCommand(Prefix + "watch attendees")
                    .Description("Monitors the attendee list for the lan" +
                                 $" **Bot Owner Only!**| `{Prefix}watchattendees [lan]`")
                    .Parameter("lan", Discord.Commands.ParameterType.Required)
                    .Do(async e =>
                    {
                        if (!NadekoBot.IsOwner(e.User.Id)) return;
                        var serverId = e.Server.Id;
                        var channelId = e.Channel.Id;
                        uint lanNumber = 0;
                        if (!uint.TryParse(e.GetArg("lan")?.Trim() ?? "", out lanNumber))
                        {
                            await e.Channel.SendMessage("`Failed to parse LAN number`");
                            return;
                        }

                        if (lanNumber < 25)
                        {
                            await e.Channel.SendMessage("`Sorry pal, dont have that. Lan 25+ only. Blame the fact we have cool new shit`");
                            return;
                        }

                        var isWatching = watchedLans.Where(w => w.ChannelId == channelId && serverId == w.ServerId && lanNumber == w.LanId).Count() > 0;

                        if (isWatching)
                        {
                            await e.Channel.SendMessage("`That lan is already been watched!`");
                            return;
                        }
                        else
                        {
                            watchedLans.Add(new WatchedLan() { ChannelId = channelId, ServerId = serverId, LanId = lanNumber });
                            await e.Channel.SendMessage($"`I am now watching LanOps {lanNumber} for new attendees.`");
                            SaveConfig();
                            return;
                        }
                    });

                cgb.CreateCommand(Prefix + "stop watching attendees")
                  .Description("Stops monitoring the attendee list for the lan" +
                               $" **Bot Owner Only!**| `{Prefix}watchattendees [lan]`")
                  .Parameter("lan", Discord.Commands.ParameterType.Required)
                  .Do(async e =>
                  {
                      if (!NadekoBot.IsOwner(e.User.Id)) return;
                      var serverId = e.Server.Id;
                      var channelId = e.Channel.Id;
                      uint lanNumber = 0;
                      if (!uint.TryParse(e.GetArg("lan")?.Trim() ?? "", out lanNumber))
                      {
                          await e.Channel.SendMessage("`Failed to parse LAN number`");
                          return;
                      }

                      if (lanNumber < 25)
                      {
                          await e.Channel.SendMessage("`Sorry pal, dont have that. Lan 25+ only. Blame the fact we have cool new shit`");
                          return;
                      }

                      var isWatching = watchedLans.Where(w => w.ChannelId == channelId && serverId == w.ServerId && lanNumber == w.LanId);

                      if (isWatching.Count() > 0)
                      {
                          watchedLans.Remove(isWatching.First());
                          await e.Channel.SendMessage($"`I am no longer watching LanOps {lanNumber} for new attendees in this channel.`");
                          SaveConfig();
                          return;
                      }
                      else
                      {
                          await e.Channel.SendMessage($"`I am already not watching LanOps {lanNumber} for new attendees in this channel.`");
                          return;
                      }
                  });
            });

            System.Threading.Tasks.Task.Run(WatchForNewAttendees);
        }

        private async Task<List<Participant>> GetAttendeeList(uint lanNumber)
        {
            var client = new HttpClient();
            var result = await client.GetAsync($"http://www.lanops.co.uk/api/events/{lanNumber}/participants");
            var content = await result.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<List<Participant>>(content).OrderBy(x=>x, new ParticiantComparer()).ToList();
        }


        public class ParticiantComparer : IComparer<Participant>
        {
            public int Compare(Participant x, Participant y)
            {
                var a = 0;
                var b = 0;

                if(!string.IsNullOrEmpty(x.Seat) && x.Seat.Length == 2)
                {
                    a = (int)x.Seat[0] * 1000 + (int)x.Seat[1];
                }
                else
                {
                    a = 1000000000;
                }

                if (!string.IsNullOrEmpty(y.Seat) && y.Seat.Length == 2)
                {
                    b = (int)y.Seat[0] * 1000 + (int)y.Seat[1];
                }
                else
                {
                    b = 1000000000;
                }

                var val =  a.CompareTo(b);
                if (val == 0 && !string.IsNullOrWhiteSpace(x.User.SteamName) && !string.IsNullOrWhiteSpace(y.User.SteamName))
                    return x.User.SteamName.CompareTo(y.User.SteamName);
                return val;
            }
        }

        private async System.Threading.Tasks.Task WatchForNewAttendees()
        {
            Dictionary<string, List<Participant>> lastCheck = new Dictionary<string, List<Participant>>();
            while(true)
            {
                try
                {
                    foreach (var watch in watchedLans.ToList())
                    {
                        var newAttendeeList = await GetAttendeeList(watch.LanId);
                        var key = $"{watch.ServerId}-{watch.ChannelId}-{watch.LanId}";

                        if (!lastCheck.Keys.Contains(key))
                            lastCheck.Add(key, newAttendeeList);
                        else
                        {
                            var lastCheckList = lastCheck[key];
                            foreach (var user in newAttendeeList)
                            {
                                var oldUserList = lastCheckList.Where(u => u.Id == user.Id);
                                if (oldUserList.Count() == 0)
                                {
                                    var server = NadekoBot.Client.GetServer(watch.ServerId);
                                    if (server == null)
                                        continue;
                                    var channel = server?.GetChannel(watch.ChannelId);
                                    if (channel == null)
                                        continue;

                                    if(string.IsNullOrWhiteSpace(user.Seat))
                                        await channel.SendMessage($"New attendee: {user.User.SteamName}.".Replace("@", "\\@"));
                                    else
                                        await channel.SendMessage($"New attendee: {user.User.SteamName} Seat: {user.Seat}.".Replace("@", "\\@"));
                                }
                                else if(oldUserList.First().Seat != user.Seat)
                                {
                                    var server = NadekoBot.Client.GetServer(watch.ServerId);
                                    if (server == null)
                                        continue;
                                    var channel = server?.GetChannel(watch.ChannelId);
                                    if (channel == null)
                                        continue;
                                    await channel.SendMessage($"Attendee {user.User.SteamName} changed seat from {oldUserList.First().Seat} to {user.Seat}.".Replace("@", "\\@"));
                                }
                            }

                            lastCheck.Remove(key);
                            lastCheck.Add(key, newAttendeeList);
                        }
                    }

                    
                    Console.WriteLine(DateTime.Now.ToShortTimeString() +  " Checked for lan attendees..");
                }
                catch(Exception e)
                {
                    Console.WriteLine(DateTime.Now.ToShortTimeString() + "Failed checking for attendees.." + e.Message);
                }
                
                await System.Threading.Tasks.Task.Delay(1000 * 60 * 30);

                //  await Task.Delay(1000 * 60 * 30);
            }
        }
    }
}
