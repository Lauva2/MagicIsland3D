using TMPro;
using MagicIsland.UI.Models;
using UnityEngine;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class MapView : UIView
    {
        public const int MaximumLocations = 12;
        public Button CloseButton;
        public Button[] LocationButtons, MarkerButtons;
        public TMP_Text[] LocationLabels, MarkerLabels;
        public TMP_Text RegionText, DetailTitle, DetailText;
        public RectTransform MapArea, PlayerMarker;
        public IslandMapGraphic MapGraphic;
        public bool IsValid => CloseButton != null && RegionText != null && DetailTitle != null && DetailText != null && MapArea != null && PlayerMarker != null && MapGraphic != null && MapGraphic.GetComponent<CanvasRenderer>() != null
            && LocationButtons?.Length == MaximumLocations && MarkerButtons?.Length == MaximumLocations && LocationLabels?.Length == MaximumLocations && MarkerLabels?.Length == MaximumLocations
            && System.Array.TrueForAll(LocationButtons, b => b != null) && System.Array.TrueForAll(MarkerButtons, b => b != null)
            && System.Array.TrueForAll(LocationLabels, b => b != null) && System.Array.TrueForAll(MarkerLabels, b => b != null);
        private void Position(RectTransform rect, Vector2 position) => rect.anchoredPosition = new Vector2(position.x * MapArea.rect.width, (position.y - 1) * MapArea.rect.height);
        public void Render(MapModel model)
        {
            MapGraphic.SetMap(model.FarmSize, model.Roads); RegionText.text = model.Region; DetailTitle.text = model.DetailTitle; DetailText.text = model.DetailText;
            for (int i = 0; i < MaximumLocations; i++)
            {
                bool active = i < model.Locations.Count; LocationButtons[i].gameObject.SetActive(active); MarkerButtons[i].gameObject.SetActive(active); if (!active) continue;
                var entry = model.Locations[i]; LocationLabels[i].text = (i + 1) + "  " + entry.Name; MarkerLabels[i].text = (i + 1).ToString();
                Color color = i == model.Selected ? new Color(.94f, .78f, .40f) : new Color(.86f, .89f, .77f);
                LocationButtons[i].targetGraphic.color = color; MarkerButtons[i].targetGraphic.color = i == model.Selected ? color : new Color(.26f, .42f, .30f);
                MarkerLabels[i].color = i == model.Selected ? new Color(.20f, .29f, .23f) : new Color(.98f, .96f, .88f);
                Position(MarkerButtons[i].GetComponent<RectTransform>(), entry.Position);
            }
            Position(PlayerMarker, model.PlayerPosition); PlayerMarker.SetAsLastSibling();
        }
    }
}
