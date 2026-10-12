using System;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class DialogueController : IUIController
    {
        private readonly DialogueView view;
        private readonly DialogueManager dialogue;
        private readonly UnityAction next, close, gift, story;
        public DialogueModel Model { get; } = new DialogueModel();
        public DialogueController(DialogueView view, DialogueManager dialogue, Action next, Action close, Action gift, Action story)
        { this.view = view; this.dialogue = dialogue; this.next = () => next(); this.close = () => close(); this.gift = () => gift(); this.story = () => story(); view.NextButton.onClick.AddListener(this.next); view.CloseButton.onClick.AddListener(this.close); view.GiftButton.onClick.AddListener(this.gift); view.StoryButton.onClick.AddListener(this.story); Tick(); }
        public void Tick()
        {
            if (view == null || dialogue.State == null || !dialogue.State.Active) return;
            var state = dialogue.State; var npc = dialogue.NPCs.Catalog.Find(state.NpcId);
            bool eventActive = dialogue.ActiveEventId != null;
            var nextEvent = dialogue.Stories?.Next(state.NpcId);
            Model.Speaker = npc.DisplayName + " · " + (eventActive ? dialogue.Stories.Catalog.Find(dialogue.ActiveEventId).DisplayName : npc.Role); Model.Text = state.Text; Model.Progress = (state.Index + 1) + " / " + state.Lines.Count + " · ESC 关闭";
            Model.NextLabel = state.LastLine ? "结束交谈" : "下一句";
            Model.Relationship = dialogue.Relationships?.Status(state.NpcId) ?? ""; Model.CanOpenGift = !eventActive && dialogue.NPCs.State.HasMet(state.NpcId);
            Model.GiftLabel = eventActive ? "事件交谈中" : Model.CanOpenGift ? "送礼" : "先完成交谈";
            Model.CanOpenStory = !eventActive && nextEvent != null && dialogue.Stories.State.Available(nextEvent.Id);
            Model.StoryLabel = eventActive ? "正在聆听" : nextEvent == null ? "往事已听完" : Model.CanOpenStory ? nextEvent.DisplayName : "往事 · 需 " + nextEvent.RequiredPoints;
            view.Render(Model);
        }
        public void Dispose() { if (view == null) return; view.NextButton.onClick.RemoveListener(next); view.CloseButton.onClick.RemoveListener(close); view.GiftButton.onClick.RemoveListener(gift); view.StoryButton.onClick.RemoveListener(story); }
    }
}
