using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.TaskItem.Models
{
    class TaskRepository
    {
        public TaskRepository()
        {
            Tasks = new List<TaskItem>();
            Monitored = new List<MonitoredLists>();
            LastTask = 1;
        }

        public List<TaskItem> Tasks { set; get; }
        public List<MonitoredLists> Monitored { set; get; }
        public int LastTask { set; get; }
        private const string dataPath = "data/tasks.json";

        public string GetNextId()
        {
            return (LastTask++).ToString();
        }


        public void Load()
        {
            if (File.Exists(dataPath))
            {
                var saved = JsonConvert.DeserializeObject<TaskRepository>(File.ReadAllText(dataPath));
                this.Tasks = saved.Tasks;
                this.LastTask = saved.LastTask;
                this.Monitored = saved.Monitored;
            }
        }

        public void Save()
        {
            lock (this)
            {
                File.WriteAllText(dataPath, JsonConvert.SerializeObject(this));
            }
        }
    }
}
