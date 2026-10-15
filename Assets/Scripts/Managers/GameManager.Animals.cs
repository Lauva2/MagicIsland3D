using System;
using System.Collections;
using MagicIsland.Interaction;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public AnimalManager Animals { get; private set; }
        private bool animalOpening, animalCommitting;
        private bool coopOffice;
        private int animalGeneration;
        private bool AnimalBusy => animalOpening || animalCommitting || UI.HasCoop || UI.HasAnimalShop;
        public bool AtAnimalOffice => !Housing.Inside && Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        private bool AtAnimalShop => !Housing.Inside && Shop.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        private bool AtCoop => !Housing.Inside && Animals.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        private void OpenCoopFromOrders() { if (orderOpening || !UI.HasOrder || !AtAnimalOffice) return; CloseOrder(); OpenCoop(true); }
        private void OpenAnimalsFromShop() { if (shopOpening || !UI.HasShop || !AtAnimalShop) return; CloseShop(); OpenAnimalShop(); }
        public void OpenCoop(bool office = false)
        { if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !(office ? AtAnimalOffice : AtCoop)) return; coopOffice = office; animalOpening = true; SetInputEnabled(false); StartCoroutine(LoadAnimalPanel(false, office, ++animalGeneration)); }
        public void OpenAnimalShop()
        { if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !AtAnimalShop) return; animalOpening = true; SetInputEnabled(false); StartCoroutine(LoadAnimalPanel(true, false, ++animalGeneration)); }
        private IEnumerator LoadAnimalPanel(bool shop, bool office, int generation)
        {
            if (shop) yield return UI.ShowAnimalShop(Animals, BuyChicken, BuyAnimalFeed, SellEgg, CloseAnimalPanel, SaveAnimals, SellQualityEgg);
            else yield return UI.ShowCoop(Animals, office, BuildCoop, FeedChickens, CollectEggs, CloseAnimalPanel, SaveAnimals, PetChicken);
            if (generation != animalGeneration) yield break; animalOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !(shop ? UI.HasAnimalShop : UI.HasCoop))
            { CloseAnimalPanel(); Interactions.SetFeedback("暂时无法打开养殖面板，请重试"); }
        }
        public void CloseAnimalPanel()
        { if (animalCommitting) return; animalGeneration++; animalOpening = false; coopOffice = false; UI.CloseCoop(); UI.CloseAnimalShop(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        public void BuildCoop() { if (UI.HasCoop && coopOffice && AtAnimalOffice) CommitAnimal(Animals.Build); }
        public void FeedChickens() { if (UI.HasCoop && !coopOffice && AtCoop) CommitAnimal(Animals.Feed); }
        public void CollectEggs() { if (UI.HasCoop && !coopOffice && AtCoop) CommitAnimal(Animals.Collect); }
        public void PetChicken(int id) { if (UI.HasCoop && !coopOffice && AtCoop) CommitAnimal(() => Animals.Pet(id)); }
        public void BuyChicken() { if (UI.HasAnimalShop && AtAnimalShop) CommitAnimal(Animals.BuyChicken, true); }
        public void BuyAnimalFeed() { if (UI.HasAnimalShop && AtAnimalShop) CommitAnimal(Animals.BuyFeed, true); }
        public void SellEgg() { if (UI.HasAnimalShop && AtAnimalShop) CommitAnimal(Animals.SellEgg, true); }
        public void SellQualityEgg() { if (UI.HasAnimalShop && AtAnimalShop) CommitAnimal(Animals.SellQualityEgg, true); }
        private void CommitAnimal(Func<bool> action, bool trade = false)
        {
            if (State != GameFlowState.Playing || animalOpening || animalCommitting || Tools.Busy) return;
            animalCommitting = true;
            try { if (action()) { if (trade) Audio.PlaySound(MagicIsland.Configuration.AudioCue.Trade); Interactions.SetFeedback(Animals.Feedback); } UI.RefreshViews(); }
            finally { animalCommitting = false; }
        }
        public void SaveAnimals()
        {
            Interactions.SetFeedback("进度只在每天睡觉结算后保存");
        }
    }
}
