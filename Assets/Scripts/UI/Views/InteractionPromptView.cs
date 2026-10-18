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
            float alpha = model.Visible ? 1 : 0;
            if (Group.alpha != alpha) Group.alpha = alpha;
            if (NameText.text != model.Name) NameText.text = model.Name;
            if (OperationText.text != model.Operation) OperationText.text = model.Operation;
            if (DetailsText.text != model.Details) DetailsText.text = model.Details;
            if (StatusText.text != model.Status) StatusText.text = model.Status;
            var color = !model.InRange ? new Color(.55f, .62f, .59f) : model.Ready ? new Color(.34f, .57f, .36f) : new Color(.68f, .46f, .22f);
            if (StatusMark.color != color) StatusMark.color = color;
            if (StatusText.color != color) StatusText.color = color;
        }
    }
}
