using TMPro;
using MagicIsland.UI.Models;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        public Button SelectButton;
        public Image Icon;
        public TMP_Text NameText;
        public TMP_Text QuantityText;
        public void Render(InventorySlotModel model)
        {
            SelectButton.GetComponent<Image>().color = model.Selected
                ? new Color(0.74f, 0.83f, 0.65f) : new Color(0.92f, 0.89f, 0.8f);
            Icon.enabled = model.Quantity > 0 && model.Icon != null;
            Icon.sprite = model.Icon;
            Icon.color = model.IconColor;
            NameText.text = model.Quantity > 0 ? model.Name : "空格";
            QuantityText.text = model.Quantity > 0 ? model.Quantity.ToString() : string.Empty;
        }
    }
}
