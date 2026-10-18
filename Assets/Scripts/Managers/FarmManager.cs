using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Tasks;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class FarmManager : MonoBehaviour
    {
        public FarmGrid Grid { get; private set; }
        public FarmState State => Grid != null ? Grid.State : null;
        public InventoryManager Inventory { get; private set; }
        public string SeedItemId => actions?.SelectedCrop.SeedId;
        public string CropItemId => actions?.SelectedCrop.HarvestId;
        public CropCatalog Crops { get; private set; }
        public CalendarManager Calendar { get; private set; }
        public FertilizerManager Fertilizer { get; internal set; }
        public string LastError { get; private set; }
        public int LoadedVisualCount => visualPrefabs.Count;
        public GameObject PlotPrefab { get; private set; }
        public event Action SeedChanged;
        public event Action<TaskAction> ActionSucceeded;
        private FarmActions actions;
        internal void SetHarvestRecorder(Action recorder) => actions?.SetHarvestRecorder(recorder);
        internal void SetMagicHarvestRecorder(Action<string> recorder) => actions?.SetMagicHarvestRecorder(recorder);
        private readonly Dictionary<string, GameObject> visualPrefabs = new Dictionary<string, GameObject>();
        private readonly List<AsyncOperationHandle<GameObject>> visualHandles = new List<AsyncOperationHandle<GameObject>>();
        private int loadGeneration;

        public IEnumerator LoadVisuals(CropCatalog crops, string plotAddress = "FarmPlot")
        {
            ReleaseVisuals(); LastError = null;
            int generation = loadGeneration;
            var plotHandle = Addressables.LoadAssetAsync<GameObject>(plotAddress);
            visualHandles.Add(plotHandle); yield return plotHandle;
            if (generation != loadGeneration) yield break;
            if (plotHandle.Status != AsyncOperationStatus.Succeeded || plotHandle.Result == null
                || plotHandle.Result.GetComponent<FarmPlotVisual>() == null || !plotHandle.Result.GetComponent<FarmPlotVisual>().IsValid
                || plotHandle.Result.GetComponentsInChildren<Collider>(true).Length != 0)
            { LastError = "Cannot load farm plot visual: " + plotAddress; ReleaseVisuals(); yield break; }
            PlotPrefab = plotHandle.Result;
            foreach (CropConfig crop in crops.Crops)
                foreach (CropGrowthStage stage in Enum.GetValues(typeof(CropGrowthStage)))
                {
                    string address = crop.PrefabAddress(stage);
                    if (visualPrefabs.ContainsKey(address)) continue;
                    var handle = Addressables.LoadAssetAsync<GameObject>(address);
                    visualHandles.Add(handle);
                    yield return handle;
                    if (generation != loadGeneration) yield break;
                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null
                        || handle.Result.GetComponentsInChildren<Renderer>(true).Length == 0
                        || handle.Result.GetComponentsInChildren<Collider>(true).Length != 0)
                    {
                        LastError = "Cannot load crop model without colliders: " + address;
                        ReleaseVisuals(); yield break;
                    }
                    visualPrefabs.Add(address, handle.Result);
                }
        }
        public void Initialize(FarmGrid grid, GameConfig config, InventoryManager inventory, StaminaManager stamina, CropCatalog crops, CalendarManager calendar = null, FertilizerConfig fertilizer = null, int qualitySeed = 1)
        {
            Grid = grid;
            Inventory = inventory;
            Crops = crops;
            Calendar = calendar;
            grid.Initialize(config.FarmSize, config.CropGrowthDays, crops, crops.FindBySeed(config.SeedItemId).Id, visualPrefabs, fertilizer?.Definition(), qualitySeed, PlotPrefab);
            actions = new FarmActions(State, inventory.State, config.SeedItemId, config.HarvestItemId, stamina.State,
                config.HoeStaminaCost, config.SeedStaminaCost, config.WaterStaminaCost, config.HarvestStaminaCost, crops.Definitions(),
                crop => Calendar == null || Calendar.CanPlant(Crops.Find(crop.Id)));
        }
        public void AdvanceDay() { if (Grid != null) { State.AdvanceDay(); Grid.Refresh(); } }
        public void ApplyRain() { if (State != null && State.WaterPlanted()) Grid.Refresh(); }
        public bool TryAct(int index, FarmTool tool, out string message)
        {
            bool harvest = State.Cells[index].Mature;
            bool acted = actions.TryAct(index, tool, out message);
            if (acted)
            {
                Grid.Refresh();
                ActionSucceeded?.Invoke(harvest ? TaskAction.Harvest : tool == FarmTool.Hoe ? TaskAction.Hoe
                    : tool == FarmTool.Seeds ? TaskAction.Plant : TaskAction.Water);
            }
            return acted;
        }
        public bool CanPerform(int index, FarmTool tool) => State != null && index >= 0 && index < State.Cells.Length
            && (tool == FarmTool.Fertilizer ? Fertilizer.State.Check(index) == FertilizerResult.Success : actions.CanPerform(index, tool));
        public string Describe(int index, FarmTool tool) => tool == FarmTool.Fertilizer ? Fertilizer.Describe(index) : actions.Describe(index, tool);
        public bool SelectSeed(string id, bool requireStock = true)
        { if (actions == null || !actions.SelectSeed(id, requireStock)) return false; SeedChanged?.Invoke(); return true; }
        public long TotalHarvest
        { get { long count = 0; foreach (CropConfig crop in Crops.Crops) count += Inventory.GetCount(crop.HarvestItemId) + Inventory.GetCount(crop.QualityHarvestItemId); return count; } }
        private void ReleaseVisuals()
        {
            loadGeneration++;
            foreach (var handle in visualHandles)
                if (handle.IsValid())
                {
                    if (handle.IsDone) Addressables.Release(handle);
                    else handle.Completed += operation => { if (operation.IsValid()) Addressables.Release(operation); };
                }
            visualHandles.Clear(); visualPrefabs.Clear(); PlotPrefab = null;
        }
        public void Clear()
        {
            if (Grid != null) { Grid.ClearCropVisuals(); Grid.ClearPlotVisuals(); }
            Grid = null; Inventory = null; actions = null; Crops = null; Calendar = null; ReleaseVisuals();
        }
        private void OnDestroy() => Clear();
    }
}
