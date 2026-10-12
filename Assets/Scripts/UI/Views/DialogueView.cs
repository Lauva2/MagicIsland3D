using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class DialogueView : UIView
    {
        public TMP_Text SpeakerText, BodyText, ProgressText, RelationshipText;
        public Button NextButton, CloseButton, GiftButton, StoryButton;
        public void Render(DialogueModel model)
        {
            SpeakerText.text = model.Speaker; BodyText.text = model.Text; ProgressText.text = model.Progress; NextButton.GetComponentInChildren<TMP_Text>().text = model.NextLabel;
            RelationshipText.text = model.Relationship; GiftButton.interactable = model.CanOpenGift; GiftButton.GetComponentInChildren<TMP_Text>().text = model.GiftLabel;
            StoryButton.interactable = model.CanOpenStory; StoryButton.GetComponentInChildren<TMP_Text>().text = model.StoryLabel;
        }
    }
}
