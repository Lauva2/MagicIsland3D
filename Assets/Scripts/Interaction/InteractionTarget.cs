using UnityEngine;

namespace MagicIsland.Interaction
{
    public enum InteractionKind { None, Farm, Home, Bed, Decoration, Fishing, Coop, Gathering, NPC, Cooking, Shipping, Storage, Workbench, Orders, Shop, Orchard, BeachForage }
    public sealed class InteractionTarget
    {
        public static readonly InteractionTarget None = new InteractionTarget(InteractionKind.None, null, "", "", "", false, false);
        public readonly InteractionKind Kind;
        public readonly Transform Root;
        public readonly string Name, Operation, Details, Id;
        public readonly bool InRange, Ready;
        public readonly int Index;
        public bool Visible => Kind != InteractionKind.None && Root != null && Root.gameObject.activeInHierarchy;
        public string Hint => !InRange ? "距离太远，请靠近" : !Ready ? Details : Operation + (string.IsNullOrEmpty(Details) ? "" : "\n" + Details);
        public InteractionTarget(InteractionKind kind, Transform root, string name, string operation, string details, bool near, bool ready, int index = -1, string id = null)
        { Kind = kind; Root = root; Name = name; Operation = operation; Details = details; InRange = near; Ready = ready; Index = index; Id = id; }
    }
}
