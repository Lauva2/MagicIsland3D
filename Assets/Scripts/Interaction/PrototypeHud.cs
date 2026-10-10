using TMPro;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Interaction
{
    // Keep this view's script identity so existing prefab references remain intact.
    public sealed class PrototypeHud : UIView
    {
        public TMP_Text DayText;
        public TMP_Text TimeText;
        public TMP_Text WeatherText;
        public TMP_Text SeedText;
        public TMP_Text InventoryText;
        public TMP_Text GoldText;
        public TMP_Text StaminaText;
        public Image StaminaFill;
        public TMP_Text HintText;
        public TMP_Text FeedbackText;
        public Button NextDayButton;
        public Button ReturnMenuButton;
        public Button InventoryButton;
        public Button[] ToolButtons;
        public Color SelectedToolColor = new Color(0.28f, 0.43f, 0.31f);
        public Color IdleToolColor = new Color(0.92f, 0.89f, 0.8f);
        public Color SelectedLabelColor = new Color(1f, 0.98f, 0.9f);
        public Color IdleLabelColor = new Color(0.22f, 0.3f, 0.23f);

        public void Render(FarmHudModel model)
        {
            DayText.text = model.Date;
            TimeText.text = model.Time;
            WeatherText.text = model.Weather;
            SeedText.text = "播种：" + model.SeedName;
            InventoryText.text = $"种子 {model.Seeds}  作物 {model.Harvest}  木材 {model.Wood}  石料 {model.Stone}";
            GoldText.text = $"金币 {model.Gold}";
            StaminaText.text = $"体力 {model.Stamina} / {model.MaxStamina}";
            StaminaFill.fillAmount = (float)model.Stamina / model.MaxStamina;
            StaminaFill.color = model.Stamina <= model.MaxStamina * 0.2f
                ? new Color(0.83f, 0.49f, 0.34f) : new Color(0.44f, 0.66f, 0.45f);
            HintText.text = model.Hint;
            FeedbackText.text = model.Feedback;
            for (int i = 0; i < ToolButtons.Length; i++)
            {
                bool selected = (int)model.Tool == i;
                ToolButtons[i].GetComponent<Image>().color = selected ? SelectedToolColor : IdleToolColor;
                ToolButtons[i].GetComponentInChildren<TMP_Text>().color = selected ? SelectedLabelColor : IdleLabelColor;
            }
        }
    }
}
