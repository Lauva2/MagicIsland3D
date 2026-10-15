using MagicIsland.Farming;
using MagicIsland.Configuration;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class FertilizerManager : MonoBehaviour
    {
        public FertilizerState State { get; private set; }
        public FertilizerConfig Config { get; private set; }
        private FarmManager farm;
        public void Initialize(FertilizerConfig config, FarmManager farm, InventoryManager inventory, StaminaManager stamina)
        { Clear(); Config = config; this.farm = farm; State = new FertilizerState(farm.State, inventory.State, stamina.State); farm.Fertilizer = this; }
        public string Describe(int index) => Explain(State.Check(index));
        public bool Apply(int index, out string message)
        { var result = State.Apply(index); message = Explain(result); if (result == FertilizerResult.Success) farm.Grid.Refresh(); return result == FertilizerResult.Success; }
        public static string Explain(FertilizerResult result) => result == FertilizerResult.Success ? "施肥"
            : result == FertilizerResult.Untilled ? "先用锄头耕地" : result == FertilizerResult.AlreadyApplied ? "本轮已施肥"
            : result == FertilizerResult.Mature ? "已成熟，请先收获" : result == FertilizerResult.MissingFertilizer ? "肥料不足，请到工作台制作"
            : result == FertilizerResult.Exhausted ? "体力不足，请到床边休息" : result == FertilizerResult.Busy ? "正在操作，请稍候" : "请选择有效农田";
        public void Clear() { if (farm != null) farm.Fertilizer = null; farm = null; State = null; Config = null; }
        private void OnDestroy() => Clear();
    }
}
