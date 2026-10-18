using System.Collections;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public MapManager Map { get; private set; }
        private bool mapOpening;
        private int mapGeneration;
        private bool MapBusy => mapOpening || UI.HasMap;
        public void ToggleMap()
        {
            if (MapBusy) { CloseMap(); return; }
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || AnyGameplayCommit || !Map.Ready) return;
            Map.PrepareOpen(); mapOpening = true; SetInputEnabled(false); StartCoroutine(LoadMap(++mapGeneration));
        }
        private IEnumerator LoadMap(int generation)
        {
            yield return UI.ShowMap(Map, CloseMap);
            if (generation != mapGeneration) yield break;
            mapOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasMap)
            { CloseMap(); Interactions.SetFeedback("暂时无法打开地图，请重试"); }
        }
        public void CloseMap()
        {
            mapGeneration++; mapOpening = false; UI.CloseMap();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
    }
}
