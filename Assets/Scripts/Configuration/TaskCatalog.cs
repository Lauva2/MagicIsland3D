using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Tasks;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [Serializable]
    public sealed class TaskConfig
    {
        public string Id, Title;
        [TextArea] public string Description;
        public TaskAction Action;
        [Min(1)] public int Required = 1;
        [Min(1)] public int GoldReward = 10;
        public TaskDefinition Definition() => new TaskDefinition(Id, Action, Required, GoldReward);
    }
    [CreateAssetMenu(menuName = "Magic Island/Task Catalog")]
    public sealed class TaskCatalog : ScriptableObject
    {
        public const string Address = "TaskCatalog";
        public List<TaskConfig> Tasks = new List<TaskConfig>();
        public IEnumerable<TaskDefinition> Definitions() => Tasks.Select(task => task.Definition());
        public bool IsValid => Tasks != null && Tasks.Count > 0 && Tasks.All(task => task != null
            && !string.IsNullOrWhiteSpace(task.Id) && !string.IsNullOrWhiteSpace(task.Title) && !string.IsNullOrWhiteSpace(task.Description)
            && Enum.IsDefined(typeof(TaskAction), task.Action) && task.Required > 0 && task.GoldReward > 0)
            && Tasks.Select(task => task.Id).Distinct().Count() == Tasks.Count;
    }
}
