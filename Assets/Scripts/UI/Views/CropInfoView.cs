using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;

namespace MagicIsland.UI.Views
{
    public sealed class CropInfoView : UIView
    {
        public CanvasGroup Group;
        public TMP_Text NameText, DetailsText, HintText;
        public void Render(CropInfoModel model)
        {
            float alpha = model.Visible ? 1 : 0;
            if (Group.alpha != alpha) Group.alpha = alpha;
            if (NameText.text != model.Name) NameText.text = model.Name;
            if (DetailsText.text != model.Details) DetailsText.text = model.Details;
            if (HintText.text != model.Hint) HintText.text = model.Hint;
        }
    }
}
