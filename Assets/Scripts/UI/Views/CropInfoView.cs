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
            Group.alpha = model.Visible ? 1 : 0;
            NameText.text = model.Name; DetailsText.text = model.Details; HintText.text = model.Hint;
        }
    }
}
