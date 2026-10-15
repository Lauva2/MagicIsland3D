using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class InteractionPromptView : UIView
    {
        public CanvasGroup Group;
        public TMP_Text NameText, OperationText, DetailsText, StatusText;
        public Image StatusMark;
        public void Render(InteractionPromptModel model)
        {
            Group.alpha = model.Visible ? 1 : 0;
            NameText.text = model.Name; OperationText.text = model.Operation; DetailsText.text = model.Details; StatusText.text = model.Status;
            var color = !model.InRange ? new Color(.55f, .62f, .59f) : model.Ready ? new Color(.34f, .57f, .36f) : new Color(.68f, .46f, .22f);
            StatusMark.color = StatusText.color = color;
        }
    }
}
