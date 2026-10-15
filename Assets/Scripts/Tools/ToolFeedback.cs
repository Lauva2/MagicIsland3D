namespace MagicIsland.Tools
{
    public enum ToolActionKind { Hoe, Seed, Water, Axe, Pickaxe, Harvest, FishingRod, Fertilizer }
    public sealed class ToolFeedback
    {
        public bool Success { get; }
        public string Message { get; }
        public string Detail { get; }
        public ToolFeedback(bool success, string message, string detail = "") { Success = success; Message = message; Detail = detail; }
    }
}
