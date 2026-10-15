using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class CoopView : UIView
    {
        public TMP_Text StatusText, StockText, CostsText, FeedbackText;
        public TMP_Text GuideText;
        public Button BuildButton, FeedButton, CollectButton, CloseButton, SaveButton;
        public TMP_Text[] ChickenNameTexts, AffinityTexts, CareTexts;
        public Graphic[] HeartGraphics;
        public Slider[] AffinityBars;
        public Button[] PetButtons;
        public void Render(CoopModel model)
        {
            StatusText.text = model.Status; StockText.text = model.Stock; CostsText.text = model.Costs; FeedbackText.text = model.Feedback;
            BuildButton.interactable = model.CanBuild; FeedButton.interactable = model.CanFeed; CollectButton.interactable = model.CanCollect;
            GuideText.text = model.Guide;
            for (int i = 0; i < PetButtons.Length; i++)
            {
                ChickenNameTexts[i].text = model.Names[i]; AffinityTexts[i].text = model.Affinities[i]; CareTexts[i].text = model.Care[i];
                AffinityBars[i].value = model.Points[i]; PetButtons[i].interactable = model.CanPet[i];
                PetButtons[i].GetComponentInChildren<TMP_Text>().text = model.PetLabels[i]; HeartGraphics[i].gameObject.SetActive(model.Hearts[i]);
            }
        }
    }
}
