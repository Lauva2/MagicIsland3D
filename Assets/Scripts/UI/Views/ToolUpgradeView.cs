using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class ToolUpgradeView : UIView
    {
        public TMP_Text BalanceText, FeedbackText;
        public TMP_Text[] TitleTexts, CostTexts, EffectTexts;
        public Button[] UpgradeButtons;
        public Button CloseButton, SaveButton;
        public void Render(ToolUpgradeModel model)
        {
            BalanceText.text = model.Balance; FeedbackText.text = model.Feedback;
            for (int i = 0; i < 2; i++)
            {
                TitleTexts[i].text = model.Titles[i]; CostTexts[i].text = model.Costs[i]; EffectTexts[i].text = model.Effects[i];
                UpgradeButtons[i].interactable = model.CanUpgrade[i]; UpgradeButtons[i].GetComponentInChildren<TMP_Text>().text = model.Labels[i];
            }
        }
    }
}
