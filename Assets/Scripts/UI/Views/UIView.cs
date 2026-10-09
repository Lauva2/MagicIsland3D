using UnityEngine;

namespace MagicIsland.UI.Views
{
    public enum UILayer { Bottom, Common, PopUp, Top }

    public abstract class UIView : MonoBehaviour
    {
        [SerializeField] private UILayer layer = UILayer.Common;
        public UILayer Layer { get => layer; set => layer = value; }
        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
