using System;
using MagicIsland.Magic;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class MagicController : IUIController
    {
        private readonly MagicView view;
        private readonly MagicManager magic;
        private readonly UnityAction study, close;
        private readonly MagicModel model = new MagicModel();
        public MagicController(MagicView view, MagicManager magic, Action study, Action close)
        {
            this.view = view; this.magic = magic; this.study = () => study(); this.close = () => close();
            view.StudyButton.onClick.AddListener(this.study); view.CloseButton.onClick.AddListener(this.close); Tick();
        }
        public void Tick()
        {
            if (magic.State == null || view == null) return;
            var game = GameManager.Instance; var stage = magic.State.Stage; var config = magic.Config;
            model.Title = "魔法手札 · 月光的回响";
            model.Progress = $"启蒙进度 {(int)stage} / 3";
            model.Story = stage == MagicStage.Unstudied
                ? "旧手札里夹着一张符文石拓本。淡蓝色的纹路间，仍有几粒沉睡的种子。\n研读古老符文，唤醒你的第一株魔法植物。"
                : stage == MagicStage.GrowingMoonflowers
                ? "你认出了符文里的植物：月光花。\n从背包选择月光花种子，在农田播种并每天浇水。成熟后亲手收获，感受花中的魔力。种子不足时可到商店补充。"
                : stage == MagicStage.OfferingMoonflower
                ? "月光花回应了你的照料。将一朵花融入手札中的符文，便能读懂云与雨的语言。\n研习消耗月光花 ×1，优先使用普通品质。"
                : "符文亮起，细雨回应了你的呼唤。\n你已完成魔法启蒙！继续种植月光花、出售收成，也可以用唤雨术照料农田。";
            int side = config.RainRadius * 2 + 1;
            model.Spell = $"唤雨术 · {(magic.State.RainLearned ? "已掌握" : "尚未掌握")}\n关闭手札，将鼠标指向附近已耕作农田，按 R。\n范围 {side} × {side} 格 · 消耗体力 {config.RainStamina}\n只浇灌范围内未成熟、尚未浇水的作物。";
            model.Action = stage == MagicStage.Unstudied ? $"研读符文 · 领取种子 ×{config.GiftSeeds}" : "献上月光花 · 学习唤雨术";
            model.ShowAction = stage == MagicStage.Unstudied || stage == MagicStage.OfferingMoonflower;
            var check = magic.State.CheckStudy(game.Inventory.State);
            model.CanStudy = model.ShowAction && check == MagicResult.Success;
            model.Feedback = magic.Feedback ?? (model.ShowAction && check != MagicResult.Success ? MagicManager.Explain(check) : "月光花四季可种，成熟后会在夜间泛起柔光。");
            view.Render(model);
        }
        public void Dispose()
        { if (view == null) return; view.StudyButton.onClick.RemoveListener(study); view.CloseButton.onClick.RemoveListener(close); }
    }
}
