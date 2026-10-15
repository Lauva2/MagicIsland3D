using MagicIsland.Interaction;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public OrchardManager Orchard { get; private set; }
        public bool HarvestTree(PlacedDecoration tree)
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !Orchard.Owns(tree)) return false;
            bool success = Tools.TryHarvestFruit(Orchard, tree, out string message); Interactions.SetFeedback(message); UI.RefreshViews(); return success;
        }
    }
}
