using System.Collections;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public HousingManager Housing { get; private set; }
        private string loadedPlayerArea = HousingState.Island;
        private Vector3? loadedPlayerPosition;
        public void EnterHome() => ChangeResidence(Housing.Entrance, HousingState.Home);
        public void ExitHome() => ChangeResidence(Housing.Interior?.Exit, HousingState.Island);
        private void ChangeResidence(HomePortal portal, string destination)
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !Housing.InRange(portal, Configuration.Config.InteractionReach)
                || !Housing.State.Begin(destination)) return;
            SetState(GameFlowState.Transitioning); SetInputEnabled(false); StartCoroutine(TransitionResidence(destination));
        }
        private IEnumerator TransitionResidence(string destination)
        {
            string error = null;
            yield return UI.ShowSceneTransition(destination == HousingState.Home ? "正在进入小屋…" : "正在返回农场…");
            if (UI.LastError != null) error = "暂时无法打开场景切换画面，请重试";
            if (error == null && destination == HousingState.Home)
            {
                yield return Scenes.LoadHomeScene(Configuration.Housing.InteriorSceneAddress);
                error = Scenes.LastError;
                if (error == null && !Housing.BindInterior(Scenes.HomeScene)) error = Housing.LastError;
                if (error == null) { yield return Furniture.BindInterior(Housing.Interior); error = Furniture.LastError; }
            }
            if (error == null && !Housing.ApplyArea(destination)) error = Housing.LastError;
            if (error != null)
            { Housing.State.Abort(); Interactions.SetFeedback(error); }
            else
            {
                Time.SetBed(Housing.Inside ? Housing.Interior.Bed.transform : null);
                Weather.Tick(Time.Clock.Minute, true); Housing.UpdatePresentation();
                Interactions.SetFeedback(Housing.Inside ? "回到小屋，走近床边可以休息" : "已返回农场");
            }
            UI.CloseSceneTransition(); SetState(GameFlowState.Playing); SetInputEnabled(!ModalOpen); UI.RefreshViews();
        }
        private IEnumerator RestoreResidence()
        {
            if (loadedPlayerArea == HousingState.Home)
            {
                yield return Scenes.LoadHomeScene(Configuration.Housing.InteriorSceneAddress);
                if (Scenes.LastError != null) yield break;
                if (!Housing.BindInterior(Scenes.HomeScene)) yield break;
                yield return Furniture.BindInterior(Housing.Interior);
                if (Furniture.LastError != null) yield break;
            }
            if (!Housing.ApplyArea(loadedPlayerArea, loadedPlayerPosition)) yield break;
            Time.SetBed(Housing.Inside ? Housing.Interior.Bed.transform : null);
            Weather.Tick(Time.Clock.Minute, true); Housing.UpdatePresentation();
            loadedPlayerArea = HousingState.Island; loadedPlayerPosition = null;
        }
        public void RequestRest()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy) return;
            if (Housing.Inside)
            {
                if (Time.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) OpenSleep();
                else Interactions.SetFeedback("请走近小屋里的床休息");
            }
            else if (Housing.InRange(Housing.Entrance, Configuration.Config.InteractionReach)) EnterHome();
            else Interactions.SetFeedback("请到小屋床边休息，进入下一天");
        }
    }
}
