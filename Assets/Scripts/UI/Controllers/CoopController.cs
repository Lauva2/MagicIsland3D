using System;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class CoopController : IUIController
    {
        public CoopModel Model { get; } = new CoopModel();
        private readonly CoopView view; private readonly AnimalManager manager; private readonly bool office;
        private readonly UnityAction build, feed, collect, close, save;
        private readonly UnityAction[] pet = new UnityAction[2];
        public CoopController(CoopView view, AnimalManager manager, bool office, Action build, Action feed, Action collect, Action close, Action save, Action<int> petChicken)
        {
            this.view = view; this.manager = manager; this.office = office; this.build = () => build(); this.feed = () => feed(); this.collect = () => collect(); this.close = () => close(); this.save = () => save();
            for (int i = 0; i < pet.Length; i++) { int id = i + 1; pet[i] = () => petChicken(id); view.PetButtons[i].onClick.AddListener(pet[i]); }
            view.BuildButton.onClick.AddListener(this.build); view.FeedButton.onClick.AddListener(this.feed); view.CollectButton.onClick.AddListener(this.collect); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || manager.State == null) return; var state = manager.State; var d = state.Definition;
            Model.Status = state.Built ? "鸡舍已建成 · 鸡 " + state.Count + " / 2 · 普通蛋 " + state.Eggs + " · 优质蛋 " + state.QualityEggs : "鸡舍尚未建造 · 在办事处申请";
            Model.Stock = "今日已喂 " + (state.Count - state.Unfed) + " / " + state.Count + " 只 · 背包饲料 " + manager.Inventory.GetCount(d.FeedId) + "\n背包木材 " + manager.Inventory.GetCount("wood") + " / 石料 " + manager.Inventory.GetCount("stone") + " · 金币 " + manager.Economy.Gold;
            Model.Costs = office ? "建造：木材 " + d.Wood + " / 石料 " + d.Stone + " / 金币 " + d.BuildGold
                : "喂食：未喂 " + state.Unfed + " 只，共需 " + state.Unfed + " 份饲料";
            Model.CanBuild = office && state.CheckBuild(manager.Inventory.State, manager.Economy.State) == AnimalResult.Success && manager.SiteClear();
            bool near = !office && (GameManager.Instance?.Animals.InRange(GameManager.Instance.Players.Player.transform.position, GameManager.Instance.Configuration.Config.InteractionReach) ?? false);
            Model.CanFeed = near && state.CheckFeed(manager.Inventory.State) == AnimalResult.Success; Model.CanCollect = near && state.CheckCollect(manager.Inventory.State) == AnimalResult.Success;
            Model.Guide = "每日摸摸 +" + d.PetAffinity + " · 首次喂食 +" + d.FeedAffinity + "\n亲密度达到 " + d.QualityAffinity + "，喂食后次日有 " + d.QualityChance + "% 概率产优质蛋";
            for (int i = 0; i < pet.Length; i++)
            {
                int id = i + 1; var bird = state.Chicken(id);
                Model.Names[i] = bird == null ? "等待伙伴入住" : manager.Config.ChickenName(id);
                Model.Points[i] = bird?.Affinity ?? 0; Model.Affinities[i] = bird == null ? "到商店购买一只鸡" : "亲密度 " + bird.Affinity + " / 100 · " + AnimalState.AffinityStage(bird.Affinity);
                Model.Care[i] = bird == null ? "入住后可以喂食和摸摸" : (bird.FedDay == state.Day ? "今日已喂" : "今日未喂") + " · " + (bird.LastPetDay == state.Day ? "已摸摸" : "未摸摸");
                Model.CanPet[i] = near && state.CheckPet(id) == AnimalResult.Success;
                Model.PetLabels[i] = bird == null ? "等待入住" : bird.LastPetDay == state.Day ? "今天已摸摸" : "摸摸";
                Model.Hearts[i] = manager.Chickens.FirstOrDefault(c => c.Id == id)?.HeartVisible ?? false;
            }
            Model.Feedback = manager.Feedback ?? (office ? "建成后到商店购买鸡和饲料" : "每只鸡每天一份饲料，次日产一枚鸡蛋"); view.Render(Model);
        }
        public void Dispose() { if (view == null) return; for (int i = 0; i < pet.Length; i++) view.PetButtons[i].onClick.RemoveListener(pet[i]); view.BuildButton.onClick.RemoveListener(build); view.FeedButton.onClick.RemoveListener(feed); view.CollectButton.onClick.RemoveListener(collect); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save); }
    }
}
