using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class CookingView : UIView
    {
        public TMP_Text[] TitleTexts, IngredientTexts, ResultTexts;
        public TMP_Text FeedbackText;
        public Button[] CookButtons;
        public Button CloseButton, SaveButton;
        public ScrollRect RecipeScroll;
        public void Render(CookingModel model)
        {
            FeedbackText.text = model.Feedback;
            for (int i = 0; i < CookButtons.Length; i++)
            {
                bool visible = i < model.Titles.Length; CookButtons[i].transform.parent.gameObject.SetActive(visible); if (!visible) continue;
                TitleTexts[i].text = model.Titles[i]; IngredientTexts[i].text = model.Ingredients[i]; ResultTexts[i].text = model.Results[i];
                CookButtons[i].interactable = model.CanCook[i]; CookButtons[i].GetComponentInChildren<TMP_Text>().text = model.Labels[i];
            }
        }
    }
}
