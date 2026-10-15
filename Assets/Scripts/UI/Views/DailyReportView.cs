using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class DailyReportView : UIView
    {
        public TMP_Text DateText, IncomeText, ExpenseText, NetText, HarvestText, MatureText, EggsText, FeedbackText, ShippingText;
        public Button ContinueButton, SaveButton, ReturnMenuButton;
        public void Render(DailyReportModel model)
        {
            DateText.text = model.Date; IncomeText.text = model.Income; ExpenseText.text = model.Expense; NetText.text = model.Net;
            HarvestText.text = model.Harvest; MatureText.text = model.Mature; EggsText.text = model.Eggs; FeedbackText.text = model.Feedback;
            ShippingText.text = model.Shipping; ShippingText.rectTransform.sizeDelta = new UnityEngine.Vector2(628, UnityEngine.Mathf.Max(110, ShippingText.preferredHeight + 16));
            ContinueButton.interactable = model.CanContinue;
            SaveButton.gameObject.SetActive(model.CanRetrySleepSave); SaveButton.interactable = model.CanRetrySleepSave;
        }
    }
}
