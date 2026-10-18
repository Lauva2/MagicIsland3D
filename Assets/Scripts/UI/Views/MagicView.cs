using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class MagicView : UIView
    {
        public TMP_Text Title, Story, Progress, Spell, Feedback, ActionLabel;
        public Button StudyButton, CloseButton;
        public bool IsValid => Title != null && Story != null && Progress != null && Spell != null && Feedback != null && ActionLabel != null && StudyButton != null && CloseButton != null;
        public void Render(MagicModel model)
        {
            Title.text = model.Title; Story.text = model.Story; Progress.text = model.Progress; Spell.text = model.Spell;
            Feedback.text = model.Feedback; ActionLabel.text = model.Action;
            StudyButton.gameObject.SetActive(model.ShowAction); StudyButton.interactable = model.CanStudy;
        }
    }
}
