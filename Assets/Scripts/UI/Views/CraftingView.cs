using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class CraftingView : UIView
    {
        public TMP_Text[] NameTexts, CostTexts, ResultTexts;
        public Button[] CraftButtons;
        public TMP_Text FeedbackText;
        public Button CloseButton, SaveButton, UpgradeButton;
        public Button PreviousButton, NextButton;
        public TMP_Text PageText;
        public void RenderPage(int page, int total)
        { PageText.text = "第 " + (page + 1) + " / " + total + " 页"; PreviousButton.interactable = page > 0; NextButton.interactable = page + 1 < total; }
        public void Render(CraftingModel model)
        {
            FeedbackText.text = model.Feedback;
            for (int i = 0; i < CraftButtons.Length; i++)
            {
                bool active = i < model.Names.Length && model.Names[i] != null; CraftButtons[i].transform.parent.gameObject.SetActive(active); if (!active) continue;
                NameTexts[i].text = model.Names[i]; CostTexts[i].text = model.Costs[i]; ResultTexts[i].text = model.Results[i];
                CraftButtons[i].interactable = model.CanCraft[i]; CraftButtons[i].GetComponentInChildren<TMP_Text>().text = model.Labels[i];
            }
        }
    }
}
