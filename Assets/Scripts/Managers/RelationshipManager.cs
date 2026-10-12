using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class RelationshipManager : MonoBehaviour
    {
        public RelationshipState State { get; private set; }
        public RelationshipCatalog Catalog { get; private set; }
        public NPCManager NPCs { get; private set; }
        public InventoryManager Inventory { get; private set; }
        public string TargetId { get; private set; }
        public string Feedback { get; private set; }
        public void Initialize(RelationshipCatalog catalog, NPCManager npcs, InventoryManager inventory, int day, SavedRelationship[] saved = null)
        { Clear(); Catalog = catalog; NPCs = npcs; Inventory = inventory; State = new RelationshipState(catalog.Definitions(npcs.Catalog), npcs.State, day); if (saved != null) State.Restore(saved); }
        public bool Prepare(string id)
        { if (State.Find(id) == null || !NPCs.State.HasMet(id)) return false; if (TargetId != id) Feedback = null; TargetId = id; return true; }
        public GiftResult Check(int slot) => State.Check(TargetId, slot, Inventory.State);
        public bool Give(int slot)
        {
            var result = State.Give(TargetId, slot, Inventory.State);
            Feedback = result == GiftResult.Success ? (State.LastFavorite ? Catalog.Find(TargetId).FavoriteReply : Catalog.Find(TargetId).StandardReply) + " · 好感度 +" + State.LastGain : Explain(result);
            return result == GiftResult.Success;
        }
        public string Stage(string id) => !NPCs.State.HasMet(id) ? "未见面" : State.Find(id).Rank(State.Points(id)) == 2 ? "友好" : State.Find(id).Rank(State.Points(id)) == 1 ? "熟悉" : "初识";
        public string Status(string id) => Stage(id) + " · 好感度 " + State.Points(id) + " / " + State.Find(id).Maximum + " · "
            + (!NPCs.State.HasMet(id) ? "完成首次交谈后可送礼" : State.LastGiftDay(id) == State.Day ? "今日已送礼" : State.Points(id) == State.Find(id).Maximum ? "好感度已满" : "今日可送礼");
        public string Greeting(string id) => State.Find(id).Rank(State.Points(id)) == 2 ? Catalog.Find(id).FriendlyGreeting
            : State.Find(id).Rank(State.Points(id)) == 1 ? Catalog.Find(id).FamiliarGreeting : null;
        public static string Explain(GiftResult result) => result == GiftResult.AlreadyGiven ? "今日已送礼，明天再来吧" : result == GiftResult.AtMaximum ? "好感度已满，未消耗礼物"
            : result == GiftResult.NotMet ? "先完成首次交谈再送礼" : result == GiftResult.InvalidItem ? "请选择背包里的作物或料理" : "暂时无法赠礼";
        public void SetSaveFailed() => Feedback = "礼物已送出，保存失败，请点击保存进度重试";
        public void SetSaved() => Feedback = "送礼进度已保存";
        public void Clear() { State = null; Catalog = null; NPCs = null; Inventory = null; TargetId = null; Feedback = null; }
    }
}
