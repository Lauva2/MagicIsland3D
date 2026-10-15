using UnityEngine;

namespace MagicIsland.Farming
{
    public sealed class FarmPlotVisual : MonoBehaviour
    {
        public GameObject GrassCover, SoilBed, FertilizerGrains;
        public Renderer[] SoilRenderers;
        public Material DrySoil, WetSoil, FertilizedDrySoil, FertilizedWetSoil;
        public bool IsValid => GrassCover != null && SoilBed != null && FertilizerGrains != null
            && SoilRenderers != null && SoilRenderers.Length > 0 && System.Array.TrueForAll(SoilRenderers, r => r != null)
            && DrySoil != null && WetSoil != null && FertilizedDrySoil != null && FertilizedWetSoil != null;

        public void Render(FarmCell cell)
        {
            GrassCover.SetActive(!cell.Tilled); SoilBed.SetActive(cell.Tilled);
            FertilizerGrains.SetActive(cell.Tilled && cell.Fertilized);
            Material material = cell.Fertilized ? (cell.Watered ? FertilizedWetSoil : FertilizedDrySoil)
                : cell.Watered ? WetSoil : DrySoil;
            foreach (Renderer renderer in SoilRenderers) renderer.sharedMaterial = material;
        }
    }
}
