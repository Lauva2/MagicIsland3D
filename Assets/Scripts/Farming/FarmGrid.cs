using System.Collections.Generic;
using UnityEngine;
using MagicIsland.Configuration;

namespace MagicIsland.Farming
{
    public sealed class FarmGrid : MonoBehaviour
    {
        public const float CellSize = 1.25f;
        public FarmState State { get; private set; }
        private Renderer[] soil;
        private Transform[] crops;
        private string[] displayedAddresses;
        private Transform highlight;
        private readonly List<Material> materials = new List<Material>();
        private Material grass, dry, wet, valid, invalid;
        private CropCatalog catalog;
        private IReadOnlyDictionary<string, GameObject> visualPrefabs;

        public void Initialize(int size, int growthDays = FarmState.GrowthDays, CropCatalog catalog = null, string defaultCropId = "turnip",
            IReadOnlyDictionary<string, GameObject> visualPrefabs = null)
        {
            this.catalog = catalog;
            this.visualPrefabs = visualPrefabs;
            State = new FarmState(size, growthDays, catalog?.Definitions(), defaultCropId);
            grass = MakeMaterial(new Color(0.39f, 0.56f, 0.27f));
            dry = MakeMaterial(new Color(0.43f, 0.28f, 0.16f));
            wet = MakeMaterial(new Color(0.25f, 0.19f, 0.13f));
            valid = MakeMaterial(new Color(0.97f, 0.9f, 0.48f));
            invalid = MakeMaterial(new Color(0.95f, 0.35f, 0.29f));
            soil = new Renderer[State.Cells.Length];
            crops = new Transform[soil.Length];
            displayedAddresses = new string[soil.Length];
            for (int i = 0; i < soil.Length; i++)
            {
                GameObject tile = Primitive("Plot " + i, PrimitiveType.Cube, CellCenter(i), new Vector3(1.16f, 0.06f, 1.16f), grass);
                soil[i] = tile.GetComponent<Renderer>();
            }
            highlight = new GameObject("Target outline").transform;
            highlight.SetParent(transform, false);
            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i < 2;
                Vector3 position = horizontal ? new Vector3(0f, 0f, i == 0 ? -0.6f : 0.6f)
                    : new Vector3(i == 2 ? -0.6f : 0.6f, 0f, 0f);
                GameObject edge = Primitive("Outline", PrimitiveType.Cube, Vector3.zero,
                    horizontal ? new Vector3(1.25f, 0.035f, 0.045f) : new Vector3(0.045f, 0.035f, 1.25f), valid);
                edge.transform.SetParent(highlight, false);
                edge.transform.localPosition = position;
            }
            Refresh();
            SetHover(-1, false);
        }

        public Vector3 CellCenter(int index)
        {
            float half = (State.Size - 1) * 0.5f;
            return transform.position + new Vector3((index % State.Size - half) * CellSize, 0.045f,
                (index / State.Size - half) * CellSize);
        }

        public int Pick(Ray ray)
        {
            Plane plane = new Plane(Vector3.up, transform.position + Vector3.up * 0.08f);
            if (!plane.Raycast(ray, out float distance)) return -1;
            Vector3 local = ray.GetPoint(distance) - transform.position;
            float halfWidth = State.Size * CellSize * 0.5f;
            int x = Mathf.FloorToInt((local.x + halfWidth) / CellSize);
            int z = Mathf.FloorToInt((local.z + halfWidth) / CellSize);
            return x >= 0 && x < State.Size && z >= 0 && z < State.Size ? z * State.Size + x : -1;
        }

        public void SetHover(int index, bool inRange)
        {
            highlight.gameObject.SetActive(index >= 0);
            if (index < 0) return;
            highlight.position = CellCenter(index) + Vector3.up * 0.08f;
            foreach (Renderer edge in highlight.GetComponentsInChildren<Renderer>()) edge.sharedMaterial = inRange ? valid : invalid;
        }

        public void Refresh()
        {
            for (int i = 0; i < soil.Length; i++)
            {
                FarmCell cell = State.Cells[i];
                soil[i].sharedMaterial = !cell.Tilled ? grass : cell.Watered ? wet : dry;
                string address = cell.Planted ? catalog.Find(cell.CropId).PrefabAddress(CropStages.FromGrowth(cell.Growth, cell.RequiredGrowthDays)) : null;
                if (displayedAddresses[i] == address) continue;
                RemoveCrop(i);
                if (address == null) continue;
                if (visualPrefabs == null || !visualPrefabs.TryGetValue(address, out GameObject prefab))
                    throw new System.InvalidOperationException("Crop model not loaded: " + address);
                GameObject model = Instantiate(prefab, CellCenter(i) + Vector3.up * 0.035f, Quaternion.identity, transform);
                model.name = "Crop " + i + " - " + prefab.name;
                crops[i] = model.transform; displayedAddresses[i] = address;
            }
        }

        public Transform GetCropVisual(int index) => crops[index];
        private void RemoveCrop(int index)
        {
            if (crops[index] != null) { crops[index].gameObject.SetActive(false); Destroy(crops[index].gameObject); }
            crops[index] = null; displayedAddresses[index] = null;
        }
        public void ClearCropVisuals()
        {
            if (crops != null) for (int i = 0; i < crops.Length; i++) RemoveCrop(i);
            visualPrefabs = null;
        }

        private Material MakeMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0f);
            materials.Add(material);
            return material;
        }

        private GameObject Primitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = objectName;
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = item.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return item;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
        }
    }
}
