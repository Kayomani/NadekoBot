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
using NadekoBot.Modules.TaskItem.Models;

namespace NadekoBot.Modules.TaskItem
{
    class TaskModule : DiscordModule
    {
        public override string Prefix { get; } = NadekoBot.Config.CommandPrefixes.Tasks;

        private TaskRepository repo = null;

        private string CreateBlockFromList(string list)
        {
            var sb = new StringBuilder();
          //  sb.Append("`");
            var tasks = repo.Tasks.ToList().Where(t => t.List == list);

            if (tasks.Count() == 0)
            {
                sb.Append("**No tasks found**");
            }
            else
            {

                sb.Append($"\r\nTask list for: **{list}** \r\n");

                foreach (var task in tasks)
                {
                    if (task.Complete)
                        sb.Append("~~");
                    sb.Append($"**{task.Id}** ");
                    if (task.Due == null)
                        sb.Append($"          -            ");
                    else
                        sb.Append($" {task.Due.Value.ToShortDateString()} ");
                 
                    if (string.IsNullOrWhiteSpace(task.User.Name) || task.User.Name == "-")
                        sb.Append($" ***Unassigned*** ");
                    else
                        sb.Append($" ***{task.User.Name}*** ");
                    if (task.Complete)
                        sb.Append("~~");
                    sb.Append("           ");
                   
                    sb.Append($"`{task.Text}`");
                   
                    if (task.Comments.Count>0)
                    {
                        sb.Append($"\r\n    **Comments** ");
                        foreach(var comment in task.Comments)
                        {
                            sb.Append($"\r\n    {comment.User.Name}:   {comment.Text}");
                        }
                        sb.Append($"\r\n");
                    }

                    sb.Append($"\r\n");
                }
            }
          //  sb.Append("`");
            return sb.ToString();
        } 

        private void UpdateChannels(string list, ulong channelFrom, ulong serverFrom)
        {
            var monitoredList = repo.Monitored.Where(m => m.List == list);

            if(monitoredList.Count()>0)
            {
                foreach (var monitor in monitoredList)
                {
                    var server = NadekoBot.Client.GetServer(monitor.ServerId);
                    if (server == null)
                        continue;
                    var channel = server?.GetChannel(monitor.ChannelId);
                    if (channel == null)
                        continue;
                    channel.SendMessage(CreateBlockFromList(list));
                }
            }
            else
            {
                var server = NadekoBot.Client.GetServer(serverFrom);
                if (server == null)
                    return;
                var channel = server?.GetChannel(channelFrom);
                if (channel == null)
                    return;
                channel.SendMessage(CreateBlockFromList(list));
            }
        }

        public override void Install(ModuleManager manager)
        {
            repo = new TaskRepository();
            repo.Load();

            manager.CreateCommands("", cgb =>
            {
                cgb.CreateCommand(Prefix + "task add")
                 .Description($"Create a new task")
                 .Parameter("list", ParameterType.Optional)
                 .Parameter("due", ParameterType.Optional)
                 .Parameter("who", ParameterType.Optional)
                 .Parameter("text", ParameterType.Unparsed)
                 .Do(async e =>
                 {
                     if(e.User.Roles.Where(r => r.Name == "Staff").Count()==0)
                     {
                         await e.Channel.SendMessage("You do not have permission to do that.");
                         return;
                     }
                     
                     var list = e.GetArg("list").Trim();
                     var dueStr = e.GetArg("due").Trim();
                     var who = e.GetArg("who").Trim();
                     var text = e.GetArg("text").Trim();

                     if (string.IsNullOrWhiteSpace(list))
                     {
                         await e.Channel.SendMessage("Please enter a task list.");
                         return;
                     }

                     DateTime due;
                     if (!DateTime.TryParse(dueStr, out due) && dueStr != "-")
                     {
                         await e.Channel.SendMessage("Please enter a due date or '-'.");
                         return;
                     }

                     if (string.IsNullOrWhiteSpace(who))
                     {
                         await e.Channel.SendMessage("Please enter a person or '-'.");
                         return;
                     }

                     if (who.Contains("/20"))
                     {
                        await e.Channel.SendMessage("It looks like you entered a date in the who field!");
                        return;
                     }

                         if (string.IsNullOrWhiteSpace(text))
                     {
                         await e.Channel.SendMessage("Please enter some text for the task '-'.");
                         return;
                     }

                     var nt = new Models.TaskItem();
                     if (dueStr != "-")
                         nt.Due = due;
                     nt.Text = text;
                     nt.Id = repo.GetNextId();
                     nt.List = list;

                     if(e.Message.MentionedUsers.Count()>0)
                     {
                         var u = e.Message.MentionedUsers.First();
                         nt.User = new User() { Id = u.Id, MeantionName = u.NicknameMention, Name = u.Name };
                     }
                     else
                     {
                         if (who.StartsWith("<@"))
                         {
                             await e.Channel.SendMessage("You meantioned a user but I can't find them?! Try not meantioning them.");
                             return;
                         }
                         else
                         {
                             nt.User = new User() {  Name = who };
                         }
                     }
                     repo.Tasks.Add(nt);
                     repo.Save();
                     UpdateChannels(list, e.Channel.Id, e.Server.Id);
                 });

                cgb.CreateCommand(Prefix + "task remove")
                .Description($"Remove a new task")
                .Parameter("id", ParameterType.Optional)
                .Do(async e =>
                {
                    if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                    {
                        await e.Channel.SendMessage("You do not have permission to do that.");
                        return;
                    }
                    var id = e.GetArg("id").Trim();

                    var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                    if (task == null)
                    {
                        await e.Channel.SendMessage("Task not found.");
                    }
                    else
                    {
                        repo.Tasks.Remove(task);
                        repo.Save();
                        await e.Channel.SendMessage("Task deleted.");
                        UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                    }
                });

                cgb.CreateCommand(Prefix + "task done")
                .Description($"Marks a task a done")
                .Parameter("id", ParameterType.Optional)
                .Do(async e =>
                {
                    if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                    {
                        await e.Channel.SendMessage("You do not have permission to do that.");
                        return;
                    }
                    var id = e.GetArg("id").Trim();

                    var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                    if (task == null)
                    {
                        await e.Channel.SendMessage("Task not found.");
                    }
                    else
                    {
                        task.Complete = true;
                        repo.Save();
                        await e.Channel.SendMessage("Task marked done.");
                        UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                    }
                });

                cgb.CreateCommand(Prefix + "task unmark done")
               .Description($"Marks a task a done")
               .Parameter("id", ParameterType.Optional)
               .Do(async e =>
               {
                   if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                   {
                       await e.Channel.SendMessage("You do not have permission to do that.");
                       return;
                   }
                   var id = e.GetArg("id").Trim();

                   var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                   if (task == null)
                   {
                       await e.Channel.SendMessage("Task not found.");
                   }
                   else
                   {
                       task.Complete = false;
                       repo.Save();
                       await e.Channel.SendMessage("Task unmarked  as done.");
                       UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                   }
               });

                cgb.CreateCommand(Prefix + "task update text")
                               .Description($"Updates the text of a task")
                               .Parameter("id", ParameterType.Optional)
                               .Parameter("text", ParameterType.Unparsed)
                               .Do(async e =>
                               {
                                   if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                                   {
                                       await e.Channel.SendMessage("You do not have permission to do that.");
                                       return;
                                   }
                                   var id = e.GetArg("id").Trim();
                                   var text = e.GetArg("text").Trim();


                                   if (string.IsNullOrWhiteSpace(id))
                                   {
                                       await e.Channel.SendMessage("Please enter a task id.");
                                       return;
                                   }

                                   if (string.IsNullOrWhiteSpace(text))
                                   {
                                       await e.Channel.SendMessage("Please enter some text for the task '-'.");
                                       return;
                                   }

                                   var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                                   if (task == null)
                                   {
                                       await e.Channel.SendMessage("Task not found.");
                                   }
                                   else
                                   {
                                       task.Text = text;
                                       repo.Save();
                                       await e.Channel.SendMessage("Task updated.");
                                       UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                                   }
                               });


                cgb.CreateCommand(Prefix + "task update date")
                .Alias(Prefix + "task date update")
                            .Description($"Updates the date of the task")
                            .Parameter("id", ParameterType.Optional)
                            .Parameter("date", ParameterType.Optional)
                            .Do(async e =>
                            {
                                if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                                {
                                    await e.Channel.SendMessage("You do not have permission to do that.");
                                    return;
                                }
                                var id = e.GetArg("id").Trim();
                                var dueStr = e.GetArg("date").Trim();

                                DateTime due;
                                if (!DateTime.TryParse(dueStr, out due) && dueStr != "-")
                                {
                                    await e.Channel.SendMessage("Please enter a due date or '-'.");
                                    return;
                                }

                                var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                                if (task == null)
                                {
                                    await e.Channel.SendMessage("Task not found.");
                                }
                                else
                                {
                                    if (dueStr != "-")
                                        task.Due = due;
                                    else
                                        task.Due = null;
                                    repo.Save();
                                    await e.Channel.SendMessage("Task updated.");
                                    UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                                }
                            });

                cgb.CreateCommand(Prefix + "task update who")
                             .Description($"Updates the assignee")
                             .Parameter("id", ParameterType.Optional)
                             .Parameter("who", ParameterType.Optional)
                             .Do(async e =>
                             {
                                 if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                                 {
                                     await e.Channel.SendMessage("You do not have permission to do that.");
                                     return;
                                 }
                                 var id = e.GetArg("id").Trim();
                                 var who = e.GetArg("who").Trim();


                                 if (string.IsNullOrWhiteSpace(id))
                                 {
                                     await e.Channel.SendMessage("Please enter a task id.");
                                     return;
                                 }

                                 if (string.IsNullOrWhiteSpace(who))
                                 {
                                     await e.Channel.SendMessage("Please enter a person or '-'.");
                                     return;
                                 }

                                 var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                                 if (task == null)
                                 {
                                     await e.Channel.SendMessage("Task not found.");
                                 }
                                 else
                                 {
                                     if (e.Message.MentionedUsers.Count() > 0)
                                     {
                                         var u = e.Message.MentionedUsers.First();
                                         task.User = new User() { Id = u.Id, MeantionName = u.NicknameMention, Name = u.Name };
                                     }
                                     else
                                     {
                                         if (who.StartsWith("<@"))
                                         {
                                             await e.Channel.SendMessage("You meantioned a user but I can't find them?! Try not meantioning them.");
                                             return;
                                         }
                                         else
                                         {
                                             task.User = new User() { Name = who };
                                         }
                                     }
                                     repo.Save();
                                     await e.Channel.SendMessage("Task updated.");
                                     UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                                 }
                             });

                cgb.CreateCommand(Prefix + "task note")
                             .Description($"Adds a note to a task")
                             .Parameter("id", ParameterType.Optional)
                             .Parameter("text", ParameterType.Optional)
                             .Do(async e =>
                             {
                                 if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                                 {
                                     await e.Channel.SendMessage("You do not have permission to do that.");
                                     return;
                                 }
                                 var id = e.GetArg("id").Trim();
                                 var text = e.GetArg("text").Trim();


                                 if (string.IsNullOrWhiteSpace(id))
                                 {
                                     await e.Channel.SendMessage("Please enter a task id.");
                                     return;
                                 }

                                 if (string.IsNullOrWhiteSpace(text))
                                 {
                                     await e.Channel.SendMessage("Please enter a comment");
                                     return;
                                 }

                                 var task = repo.Tasks.Where(t => t.Id == id).FirstOrDefault();
                                 if (task == null)
                                 {
                                     await e.Channel.SendMessage("Task not found.");
                                 }
                                 else
                                 {
                                     task.Comments.Add(new Comment() { User = new User() { Id = e.User.Id, MeantionName = e.User.NicknameMention, Name = e.User.Name }, Text = text });
                                     repo.Save();
                                     await e.Channel.SendMessage("Task updated.");
                                     UpdateChannels(task.List, e.Channel.Id, e.Server.Id);
                                 }
                             });

                cgb.CreateCommand(Prefix + "task monitor")
                      .Description($"Monitors a list in this channel")
                      .Parameter("list", ParameterType.Optional)
                      .Do(async e =>
                      {
                          if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                          {
                              await e.Channel.SendMessage("You do not have permission to do that.");
                              return;
                          }
                          var list = e.GetArg("list").Trim();


                          if (string.IsNullOrWhiteSpace(list))
                          {
                              await e.Channel.SendMessage("Please enter a list name.");
                              return;
                          }


                          var task = repo.Monitored.Where(t => t.List == list && e.Channel.Id == t.ChannelId).FirstOrDefault();
                          if (task != null)
                          {
                              await e.Channel.SendMessage("List is already monitored on this channel.");
                          }
                          else
                          {
                              repo.Monitored.Add(new MonitoredLists() { ChannelId = e.Channel.Id, ServerId = e.Server.Id, List = list });
                              repo.Save();
                              await e.Channel.SendMessage("Task list is now monitored in here.");
                          }
                      });

                cgb.CreateCommand(Prefix + "task unmonitor")
                    .Description($"Stops monitoring a list in this channel")
                    .Parameter("list", ParameterType.Optional)
                    .Do(async e =>
                    {
                        if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                        {
                            await e.Channel.SendMessage("You do not have permission to do that.");
                            return;
                        }
                        var list = e.GetArg("list").Trim();


                        if (string.IsNullOrWhiteSpace(list))
                        {
                            await e.Channel.SendMessage("Please enter a list name.");
                            return;
                        }


                        var task = repo.Monitored.Where(t => t.List == list && e.Channel.Id == t.ChannelId).FirstOrDefault();
                        if (task == null)
                        {
                            await e.Channel.SendMessage("Task list isn't being monitored in here.");
                        }
                        else
                        {
                            repo.Monitored.Remove(task);
                            repo.Save();
                            await e.Channel.SendMessage("Task list will no longer be monitored in here.");
                        }
                    });
                cgb.CreateCommand(Prefix + "task status")
                 .Alias(Prefix + "tasks status")
                   .Alias(Prefix + "task list")
                 .Description($"Displays list status")
                 .Parameter("list", ParameterType.Optional)
                 .Do(async e =>
                 {
                     if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                     {
                         await e.Channel.SendMessage("You do not have permission to do that.");
                         return;
                     }
                     var list = e.GetArg("list").Trim();


                     if (string.IsNullOrWhiteSpace(list))
                     {
                         await e.Channel.SendMessage("Please enter a list name.");
                         return;
                     }

                     await e.Channel.SendMessage(CreateBlockFromList(list));
                 });

                cgb.CreateCommand(Prefix + "task reminder")
                  .Description($"Manually trigger reminders")
                  .Parameter("list", ParameterType.Optional)
                  .Do(async e =>
                  {
                      if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                      {
                          await e.Channel.SendMessage("You do not have permission to do that.");
                          return;
                      }
                      var list = e.GetArg("list").Trim();


                      if (string.IsNullOrWhiteSpace(list))
                      {
                          await e.Channel.SendMessage("Please enter a list name.");
                          return;
                      }

                      Reminder(list);
                  });

                cgb.CreateCommand(Prefix + "task help")
                .Description($"Help")
                .Do(async e =>
                {
                    if (e.User.Roles.Where(r => r.Name == "Staff").Count() == 0)
                    {
                        await e.Channel.SendMessage("You do not have permission to do that.");
                        return;
                    }
                    await e.Channel.SendMessage("Task commands:\r\n>task add [list] [due|-] [who|-] [text]\r\n>task remove [id]\r\n>task done [id]\r\n>task unmark done [id]\r\n>task update text [id] [text]\r\n>task update date [id] [date|-]\r\n>task update who [id] [who|-]\r\n>task note [id] [text]\r\n>task monitor [list]\r\n>task unmonitor [list]\r\n>task list [list]");
                });

            });

            System.Threading.Tasks.Task.Run(CheckReminders);
        }

        private async Task<List<Participant>> GetAttendeeList(uint lanNumber)
        {
            var client = new HttpClient();
            var result = await client.GetAsync($"http://www.lanops.co.uk/events/{lanNumber}/participants");
            var content = await result.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<List<Participant>>(content);
        }


        private void Reminder(string list = null)
        {
            foreach (var monitor in repo.Monitored)
            {
                foreach (var task in repo.Tasks.Where(t => t.List == monitor.List && !t.Complete && (monitor.List == list || string.IsNullOrEmpty(list))))
                {
                    if (task.Due != null)
                    {
                        if (DateTime.Now.AddDays(1).ToShortDateString() == task.Due.Value.ToShortDateString())
                        {
                            var server = NadekoBot.Client.GetServer(monitor.ServerId);
                            if (server == null)
                                continue;
                            var channel = server?.GetChannel(monitor.ChannelId);
                            if (channel == null)
                                continue;
                            channel.SendMessage($"Task {task.Id} is scheduled for tomorrow. Assigneee: {(task.User.Id > 0 ? task.User.MeantionName : task.User.Name)}. `{task.Text}`");
                        }
                        else if (DateTime.Now.ToShortDateString() == task.Due.Value.ToShortDateString())
                        {
                            var server = NadekoBot.Client.GetServer(monitor.ServerId);
                            if (server == null)
                                continue;
                            var channel = server?.GetChannel(monitor.ChannelId);
                            if (channel == null)
                                continue;
                            channel.SendMessage($"Task {task.Id} is scheduled for today. Assigneee: {(task.User.Id > 0 ? task.User.MeantionName : task.User.Name)}. `{task.Text}`");
                        }
                        else if (DateTime.Now > task.Due)
                        {
                            var server = NadekoBot.Client.GetServer(monitor.ServerId);
                            if (server == null)
                                continue;
                            var channel = server?.GetChannel(monitor.ChannelId);
                            if (channel == null)
                                continue;
                            channel.SendMessage($"Task {task.Id} is over due. Assigneee: {(task.User.Id > 0 ? task.User.MeantionName : task.User.Name)}. `{task.Text}`");
                        }
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task CheckReminders()
        {
            Dictionary<string, List<Participant>> lastCheck = new Dictionary<string, List<Participant>>();
            while (true)
            {
                if (DateTime.Now.Hour >= 18 && DateTime.Now.Hour < 19)
                {
                    Reminder();
                }

                await System.Threading.Tasks.Task.Delay(1000 * 60 * 45);

                //  await Task.Delay(1000 * 60 * 30);
            }
        }
    }
}
