using System;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FertilizerSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, ItemCatalog items)
        {
            const string path="Assets/Config/FertilizerConfig.asset";
            var config=AssetDatabase.LoadAssetAtPath<FertilizerConfig>(path);
            if(config==null) { config=ScriptableObject.CreateInstance<FertilizerConfig>();AssetDatabase.CreateAsset(config,path); }
            var entry=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path))??settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group);if(entry.address!=FertilizerConfig.Address)entry.SetAddress(FertilizerConfig.Address);
            var crops=AssetDatabase.LoadAssetAtPath<CropCatalog>("Assets/Config/CropCatalog.asset");bool changed=false,cropChanged=false;
            foreach(var crop in crops.Crops)
            {
                if(string.IsNullOrWhiteSpace(crop.QualityHarvestItemId)) { crop.QualityHarvestItemId="quality_"+crop.HarvestItemId;cropChanged=true; }
                if(items.Find(crop.QualityHarvestItemId)!=null)continue;
                var ordinary=items.Find(crop.HarvestItemId);long price=((long)ordinary.SellPrice*3+1)/2;if(price>int.MaxValue)throw new Exception("Crop price is too high for quality upgrade");
                items.Items.Add(new ItemConfig { Id=crop.QualityHarvestItemId,DisplayName="优质"+ordinary.DisplayName,Type=MagicIsland.Inventory.ItemType.Crop,MaxStack=ordinary.MaxStack,Icon=ordinary.Icon,IconColor=Color.Lerp(ordinary.IconColor,new Color(1,.80f,.30f),.45f),SellPrice=(int)price,Description="品质更好的收成，售价更高。料理和订单可使用，优先消耗普通作物。" });changed=true;
            }
            if(items.Find(config.ItemId)==null)
            { items.Items.Add(new ItemConfig { Id=config.ItemId,DisplayName="基础肥料",Type=MagicIsland.Inventory.ItemType.Material,MaxStack=99,Icon=items.Find("wood").Icon,IconColor=new Color(.65f,.47f,.25f),Description="选择 7 肥料，再点击已耕作农田。每轮种植可用一次，提高优质作物概率；成熟后不能施肥。" });changed=true; }
            if(changed)EditorUtility.SetDirty(items);if(cropChanged)EditorUtility.SetDirty(crops);
            var crafting=AssetDatabase.LoadAssetAtPath<CraftingCatalog>("Assets/Config/CraftingCatalog.asset");
            if(!crafting.Recipes.Any(r=>r.Id=="basic_fertilizer"))
            { var recipe=new CraftingRecipeConfig { Id="basic_fertilizer",DisplayName="基础肥料",OutputItemId=config.ItemId,Yield=5,IsSupply=true,IsFarmingSupply=true };recipe.Ingredients.Add(new CraftingIngredientConfig { ItemId="wood",Quantity=2 });crafting.Recipes.Add(recipe);EditorUtility.SetDirty(crafting); }
        }
        public static void UpgradeUi()
        {
            string craftingPath=Factory.Folder+"/CraftingPanel.prefab";var craftingRoot=PrefabUtility.LoadPrefabContents(craftingPath);
            try
            {
                var intro=craftingRoot.GetComponent<CraftingView>().PageText.transform.parent.Find("Introduction")?.GetComponent<TMP_Text>();
                if(intro!=null&&intro.text=="布置农场和小屋，也能把玉米加工成饲料") { intro.text="布置农场和小屋，制作饲料与基础肥料";PrefabUtility.SaveAsPrefabAsset(craftingRoot,craftingPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(craftingRoot); }
            string path=Factory.Folder+"/FarmHudPanel.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud=root.GetComponent<PrototypeHud>();if(hud.ToolButtons.Length<7)
                {
                    var parent=hud.ToolButtons[0].transform.parent;Array.Resize(ref hud.ToolButtons,7);hud.ToolButtons[6]=Factory.Button("Fertilizer",parent,Vector2.zero,new Vector2(94,44),"7 肥料",Factory.Sand);
                    for(int i=0;i<7;i++) { var rect=hud.ToolButtons[i].GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(16+100*i,-14);rect.sizeDelta=new Vector2(94,44);var text=hud.ToolButtons[i].GetComponentInChildren<TMP_Text>();text.rectTransform.sizeDelta=rect.sizeDelta;text.fontSize=19; }
                    hud.HintText.rectTransform.anchoredPosition=new Vector2(724,-14);hud.HintText.rectTransform.sizeDelta=new Vector2(200,44);PrefabUtility.SaveAsPrefabAsset(root,path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path=Factory.Folder+"/ShopPanel.prefab";root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<ShopView>();if(view.CropButtons.Length<8)
                {
                    var card=view.BalanceText.transform.parent;card.GetComponent<RectTransform>().sizeDelta=new Vector2(700,640);int old=view.CropButtons.Length;Array.Resize(ref view.CropButtons,8);
                    for(int i=old;i<8;i++)view.CropButtons[i]=Factory.Button("Quality Crop "+i,card,Vector2.zero,new Vector2(153,40),"优质作物",Factory.Sand);
                    for(int i=0;i<8;i++) { var rect=view.CropButtons[i].GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(32+i%4*161,-130-i/4*50);rect.sizeDelta=new Vector2(153,40);var text=view.CropButtons[i].GetComponentInChildren<TMP_Text>();text.rectTransform.sizeDelta=rect.sizeDelta;text.fontSize=19; }
                    view.BuyText.transform.parent.GetComponent<RectTransform>().anchoredPosition=new Vector2(32,-242);view.SellText.transform.parent.GetComponent<RectTransform>().anchoredPosition=new Vector2(32,-348);
                    view.StockText.rectTransform.anchoredPosition=new Vector2(32,-450);view.StockText.rectTransform.sizeDelta=new Vector2(636,60);view.FeedbackText.rectTransform.anchoredPosition=new Vector2(32,-522);
                    view.AnimalShopButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(32,-582);view.InventoryUpgradeButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(224,-582);PrefabUtility.SaveAsPrefabAsset(root,path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path=Factory.Folder+"/CropInfoPanel.prefab";root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<CropInfoView>();if(view.DetailsText.rectTransform.sizeDelta.x==270)
                { var card=view.NameText.transform.parent.GetComponent<RectTransform>();card.sizeDelta=new Vector2(350,160);view.NameText.rectTransform.sizeDelta=new Vector2(310,32);view.DetailsText.rectTransform.sizeDelta=new Vector2(310,60);view.DetailsText.fontSize=18;view.HintText.rectTransform.sizeDelta=new Vector2(310,28);view.HintText.fontSize=17;PrefabUtility.SaveAsPrefabAsset(root,path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
