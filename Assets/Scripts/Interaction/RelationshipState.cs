using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
namespace MagicIsland.Interaction
{
    public sealed class RelationshipDefinition
    {
        public string NpcId { get; }
        public string FavoriteId { get; }
        public int Maximum { get; }
        public int FamiliarAt { get; }
        public int FriendlyAt { get; }
        public int FavoriteGain { get; }
        public int FoodGain { get; }
        public int CropGain { get; }
        public RelationshipDefinition(string id, string favorite, int maximum = 100, int familiarAt = 30, int friendlyAt = 70, int favoriteGain = 20, int foodGain = 10, int cropGain = 5)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(favorite) || familiarAt < 1 || friendlyAt <= familiarAt || maximum <= friendlyAt
                || favoriteGain < 1 || foodGain < 1 || cropGain < 1 || favoriteGain > maximum || foodGain > maximum || cropGain > maximum)
                throw new ArgumentException("Invalid relationship definition");
            NpcId = id; FavoriteId = favorite; Maximum = maximum; FamiliarAt = familiarAt; FriendlyAt = friendlyAt; FavoriteGain = favoriteGain; FoodGain = foodGain; CropGain = cropGain;
        }
        public int Gain(ItemDefinition item) => item == null || (item.Type != ItemType.Food && item.Type != ItemType.Crop) ? 0
            : item.Id == FavoriteId ? FavoriteGain : item.Type == ItemType.Food ? FoodGain : CropGain;
        public int Rank(int points) => points >= FriendlyAt ? 2 : points >= FamiliarAt ? 1 : 0;
    }
    [Serializable] public sealed class SavedRelationship { public string Id; public int Points; public int LastGiftDay; }
    public enum GiftResult { Success, UnknownNpc, NotMet, AlreadyGiven, AtMaximum, InvalidItem, Busy }
    public sealed class RelationshipState
    {
        public int Day { get; private set; }
        public int LastGain { get; private set; }
        public bool LastFavorite { get; private set; }
        private readonly RelationshipDefinition[] definitions;
        private readonly NPCState npcs;
        private SavedRelationship[] records;
        private bool giving;
        public IReadOnlyList<RelationshipDefinition> Definitions { get; }
        public event Action Changed;
        public RelationshipState(IEnumerable<RelationshipDefinition> definitions, NPCState npcs, int day)
        {
            this.definitions = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions)); this.npcs = npcs ?? throw new ArgumentNullException(nameof(npcs));
            if (day < 1 || day == int.MaxValue || this.definitions.Length < 1 || this.definitions.Length > 8 || this.definitions.Any(d => d == null)
                || this.definitions.Select(d => d.NpcId).Distinct().Count() != this.definitions.Length) throw new ArgumentException("Invalid relationship state");
            foreach (var rule in this.definitions) npcs.HasMet(rule.NpcId);
            Day = day; Definitions = Array.AsReadOnly(this.definitions); records = this.definitions.Select(d => new SavedRelationship { Id = d.NpcId }).ToArray();
        }
        private int Index(string id) => Array.FindIndex(definitions, d => d.NpcId == id);
        public int Points(string id) { int index = Index(id); if (index < 0) throw new ArgumentException("Unknown NPC"); return records[index].Points; }
        public int LastGiftDay(string id) { int index = Index(id); if (index < 0) throw new ArgumentException("Unknown NPC"); return records[index].LastGiftDay; }
        public RelationshipDefinition Find(string id) { int index = Index(id); return index < 0 ? null : definitions[index]; }
        public GiftResult Check(string id, int slotIndex, InventoryState bag)
        {
            if (giving) return GiftResult.Busy;
            int index = Index(id); if (index < 0) return GiftResult.UnknownNpc;
            if (!npcs.HasMet(id)) return GiftResult.NotMet;
            if (records[index].LastGiftDay == Day) return GiftResult.AlreadyGiven;
            if (records[index].Points == definitions[index].Maximum) return GiftResult.AtMaximum;
            if (slotIndex < 0 || slotIndex >= bag.Capacity || bag.Slots[slotIndex].Empty || definitions[index].Gain(bag.FindDefinition(bag.Slots[slotIndex].ItemId)) == 0) return GiftResult.InvalidItem;
            return GiftResult.Success;
        }
        public GiftResult Give(string id, int slotIndex, InventoryState bag)
        {
            var result = Check(id, slotIndex, bag); if (result != GiftResult.Success) return result;
            giving = true;
            try
            {
                int index = Index(id); var rule = definitions[index]; var record = records[index]; var slot = bag.Slots[slotIndex];
                LastGain = Math.Min(rule.Maximum - record.Points, rule.Gain(bag.FindDefinition(slot.ItemId))); LastFavorite = slot.ItemId == rule.FavoriteId;
                bag.TryRemoveSlot(slotIndex, 1, false); record.Points += LastGain; record.LastGiftDay = Day;
                bag.NotifyChanged(); Changed?.Invoke(); return GiftResult.Success;
            }
            finally { giving = false; }
        }
        public void NextDay(int day)
        {
            if (giving || day != (long)Day + 1 || day == int.MaxValue) throw new ArgumentException("Relationships must advance with farm");
            Day = day; Changed?.Invoke();
        }
        public SavedRelationship[] Capture() => records.Select(r => new SavedRelationship { Id = r.Id, Points = r.Points, LastGiftDay = r.LastGiftDay }).ToArray();
        public void Restore(SavedRelationship[] saved)
        {
            if (giving || saved == null || saved.Length != definitions.Length) throw new ArgumentException("Invalid saved relationships");
            for (int i = 0; i < saved.Length; i++)
            {
                var record = saved[i]; if (record == null || record.Id != definitions[i].NpcId || record.Points < 0 || record.Points > definitions[i].Maximum
                    || record.LastGiftDay < 0 || record.LastGiftDay > Day || ((record.Points == 0) != (record.LastGiftDay == 0))
                    || (record.Points > 0 && !npcs.HasMet(record.Id))) throw new ArgumentException("Invalid saved relationship record");
            }
            records = saved.Select(r => new SavedRelationship { Id = r.Id, Points = r.Points, LastGiftDay = r.LastGiftDay }).ToArray(); Changed?.Invoke();
        }
    }
}
