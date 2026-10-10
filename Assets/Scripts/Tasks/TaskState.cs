using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Economy;

namespace MagicIsland.Tasks
{
    public enum TaskAction { Hoe, Plant, Water, Sleep, Harvest, Sell }
    public enum TaskClaimResult { Success, Incomplete, Finished, GoldLimit, Busy }

    public sealed class TaskDefinition
    {
        public string Id { get; }
        public TaskAction Action { get; }
        public int Required { get; }
        public int Reward { get; }
        public TaskDefinition(string id, TaskAction action, int required, int reward)
        {
            if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(TaskAction), action) || required < 1 || reward < 1)
                throw new ArgumentException("Invalid task definition");
            Id = id; Action = action; Required = required; Reward = reward;
        }
    }
    [Serializable]
    public sealed class TaskProgress
    {
        public string Id;
        public int Progress;
        public bool Claimed;
    }

    public sealed class TaskState
    {
        private readonly TaskDefinition[] definitions;
        private TaskProgress[] progress;
        private bool claiming;
        public event Action Changed;
        public int Count => definitions.Length;
        public int CurrentIndex { get { for (int i = 0; i < Count; i++) if (!progress[i].Claimed) return i; return Count; } }
        public bool Finished => CurrentIndex == Count;
        public bool Ready => !Finished && progress[CurrentIndex].Progress == definitions[CurrentIndex].Required;
        public int GetProgress(int index) => progress[index].Progress;
        public bool IsClaimed(int index) => progress[index].Claimed;
        public TaskState(IEnumerable<TaskDefinition> tasks)
        {
            definitions = tasks?.ToArray() ?? throw new ArgumentNullException(nameof(tasks));
            if (definitions.Length == 0 || definitions.Any(task => task == null) || definitions.Select(task => task.Id).Distinct().Count() != definitions.Length)
                throw new ArgumentException("Missing or duplicate tasks");
            progress = definitions.Select(task => new TaskProgress { Id = task.Id }).ToArray();
        }
        public bool Record(TaskAction action, int amount = 1)
        {
            if (claiming || amount < 1 || !Enum.IsDefined(typeof(TaskAction), action)) return false;
            bool changed = false;
            // Remember early successful work, so completing a later goal before claiming is never lost.
            for (int i = 0; i < Count; i++)
                if (!progress[i].Claimed && definitions[i].Action == action && progress[i].Progress < definitions[i].Required)
                { progress[i].Progress = (int)Math.Min(definitions[i].Required, (long)progress[i].Progress + amount); changed = true; }
            if (changed) Changed?.Invoke();
            return changed;
        }
        public TaskClaimResult TryClaim(EconomyState economy)
        {
            if (claiming) return TaskClaimResult.Busy;
            int index = CurrentIndex;
            if (index == Count) return TaskClaimResult.Finished;
            if (!Ready) return TaskClaimResult.Incomplete;
            if (economy == null || !economy.CanEarn(definitions[index].Reward)) return TaskClaimResult.GoldLimit;
            claiming = true;
            try
            {
                economy.TryEarn(definitions[index].Reward, false);
                progress[index].Claimed = true;
                // Both state changes are committed before any observer is called.
                economy.NotifyChanged(); Changed?.Invoke();
                return TaskClaimResult.Success;
            }
            finally { claiming = false; }
        }
        public TaskProgress[] Capture() => progress.Select(task => new TaskProgress { Id = task.Id, Progress = task.Progress, Claimed = task.Claimed }).ToArray();
        public void Restore(TaskProgress[] saved)
        {
            if (claiming || saved == null || saved.Length != Count) throw new ArgumentException("Invalid saved tasks");
            bool gap = false;
            for (int i = 0; i < Count; i++)
            {
                TaskProgress task = saved[i];
                if (task == null || task.Id != definitions[i].Id || task.Progress < 0 || task.Progress > definitions[i].Required
                    || (task.Claimed && (gap || task.Progress != definitions[i].Required))) throw new ArgumentException("Invalid saved task progress");
                if (!task.Claimed) gap = true;
            }
            progress = saved.Select(task => new TaskProgress { Id = task.Id, Progress = task.Progress, Claimed = task.Claimed }).ToArray();
            Changed?.Invoke();
        }
    }
}
