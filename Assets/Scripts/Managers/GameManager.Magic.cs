using System.Collections;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public MagicManager Magic { get; private set; }
        private bool magicOpening, magicCommitting;
        private int magicGeneration;
        private bool MagicBusy => magicOpening || magicCommitting || UI.HasMagic;
        public void ToggleMagic()
        {
            if (MagicBusy) { CloseMagic(); return; }
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || AnyGameplayCommit || Magic.State == null) return;
            magicOpening = true; SetInputEnabled(false); StartCoroutine(LoadMagic(++magicGeneration));
        }
        private IEnumerator LoadMagic(int generation)
        {
            yield return UI.ShowMagic(Magic, StudyMagic, CloseMagic);
            if (generation != magicGeneration) yield break;
            magicOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasMagic)
            { CloseMagic(); Interactions.SetFeedback("暂时无法打开魔法手札，请重试"); }
        }
        public void CloseMagic()
        {
            if (magicCommitting) return;
            magicGeneration++; magicOpening = false; UI.CloseMagic();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void StudyMagic()
        {
            if (State != GameFlowState.Playing || magicOpening || magicCommitting || !UI.HasMagic || Tools.Busy || AnyGameplayCommit) return;
            magicCommitting = true;
            try { Magic.Study(Inventory); }
            finally { magicCommitting = false; }
        }
    }
}
