using System.IO;
using MagicIsland.UI.Views;
using MagicIsland.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class PauseSleepUiSetup
    {
        public static void CreateAndUpgrade()
        {
            string path = Factory.Folder + "/PausePanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("PausePanel");
                try
                {
                    var view = root.AddComponent<PauseView>(); view.Layer = UILayer.PopUp;
                    var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f,.2f,.15f,.58f)); shade.anchorMax=Vector2.one; shade.GetComponent<Image>().sprite=null;
                    var card = Factory.Panel("Pause Card", root.transform, Vector2.one*.5f, Vector2.one*.5f, Vector2.zero, new Vector2(520,408),Factory.Cream);Factory.SoftShadow(card);
                    view.TitleText=Factory.Label("Title",card,new Vector2(32,-22),new Vector2(456,44),"休息片刻",30);
                    view.MessageText=Factory.Label("Message",card,new Vector2(32,-84),new Vector2(456,94),"游戏已暂停\n进度仅在每天睡觉结算后保存。",20);
                    view.ContinueButton=Factory.Button("Resume",card,new Vector2(32,-204),new Vector2(456,44),"继续游戏",new Color(.7f,.81f,.62f));
                    view.SettingsButton=Factory.Button("Settings",card,new Vector2(32,-264),new Vector2(456,44),"设置",Factory.Sand);
                    view.ReturnMenuButton=Factory.Button("Return Menu",card,new Vector2(32,-324),new Vector2(456,44),"返回主菜单",Factory.Sand);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            foreach(string file in Directory.GetFiles(Factory.Folder,"*.prefab")) Upgrade(file.Replace('\\','/'));
        }
        private static void Upgrade(string path)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<UIView>(); if(view==null || view.SleepOnlyLayout) return;
                bool changed=false;
                var save=view.GetType().GetField("SaveButton")?.GetValue(view) as Button;
                if(save!=null)
                {
                    save.gameObject.SetActive(false);
                    save.GetComponentInChildren<TMP_Text>(true).text=view is DailyReportView ? "重试睡觉存档" : "睡觉时保存";
                    if(view is DailyReportView) { var rect=save.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(180,44);save.GetComponentInChildren<TMP_Text>(true).fontSize=18; }
                    changed=true;
                }
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    string value=text.text.Replace("成功存取自动保存","进度仅在睡觉时保存").Replace("成功后自动保存","进度仅在睡觉时保存").Replace("成功捕获自动保存","进度仅在睡觉时保存").Replace("成功交易自动保存","进度仅在睡觉时保存").Replace("自动保存","睡觉时保存");
                    if(value!=text.text) { text.text=value;changed=true; }
                }
                if(view is SettingsView settings)
                { settings.FeedbackText=root.transform.Find("Settings Panel/Hint").GetComponent<TMP_Text>(); settings.FeedbackText.text="设置自动保存，与游戏进度独立";changed=true; }
                if(view is PrototypeHud hud) { hud.ReturnMenuButton.GetComponentInChildren<TMP_Text>(true).text="暂停（ESC）";changed=true; }
                if(changed) { view.SleepOnlyLayout=true;PrefabUtility.SaveAsPrefabAsset(root,path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
