using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class FertilizerFlowVerification
    {
        const string Key="MagicIsland.FertilizerVerification";
        static FertilizerFlowVerification() { if(SessionState.GetBool(Key,false)) EditorApplication.update+=Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.ValidateUiPrefabs();
            string[] paths={"Assets/Art/Prefabs/UI/FarmHudPanel.prefab","Assets/Art/Prefabs/UI/ShopPanel.prefab","Assets/Art/Prefabs/UI/CropInfoPanel.prefab","Assets/Config/CropCatalog.asset","Assets/Config/ItemCatalog.asset","Assets/Config/FertilizerConfig.asset","Assets/Config/CraftingCatalog.asset"};
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Repeated setup preserves fertilizer resources");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool b,string why) { if(!b)throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key+".Phase",p);Debug.Log("Fertilizer phase "+p); }
        static T View<T>() where T:Component => UnityEngine.Object.FindObjectOfType<T>();
        static string Dir=>SessionState.GetString(Key+".Dir","");
        static void SaveDirectory(GameManager g,string dir)=>g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath),"Fertilizer and quality activities must not save during the day");
        static void Plot(GameManager g,int index)=>g.Players.RestorePosition(g.Farm.Grid.CellCenter(index)+new Vector3(0,.1f,-1));
        static void TextCheck(Component root) { foreach(var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true);Require(!t.isTextOverflowing&&t.text.Where(c=>!char.IsControl(c)).All(c=>t.font.HasCharacter(c)),"Quality TMP "+t.name+"="+t.text); } }
        static void Capture(GameManager g,string name) { g.UI.RefreshViews();Canvas.ForceUpdateCanvases();g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true); }
        static void Home(GameManager g) { g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome(); }
        static void Rest(GameManager g) { g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest(); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==21&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Fertilizer timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Fertilizer boot failure");
                if(p==0&&g.State==GameFlowState.MainMenu) { string dir=Path.GetFullPath("Logs/FertilizerSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);SessionState.SetString(Key+".Dir",dir);SaveDirectory(g,dir);g.EnterGame();Phase(1); }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    Require(g.Configuration.Fertilizer!=null&&g.Configuration.Fertilizer.NormalChance==10&&g.Configuration.Fertilizer.FertilizedChance==40&&g.Fertilizer.State!=null,"AA fertilizer configuration and manager");
                    Require(g.Configuration.Items.Find("quality_turnip").SellPrice==38&&g.Configuration.Items.Find("quality_tomato").SellPrice==75&&g.Configuration.Items.Find("quality_corn").SellPrice==128,"Quality item IDs and configured premium prices");
                    int planting=Enumerable.Range(1,500).First(n=> { int roll=FertilizerDefinition.Roll(g.Farm.State.QualitySeed,0,n);return roll>=10&&roll<40; });g.Farm.State.Cells[0].PlantingNumber=planting-1;
                    g.Inventory.TryAdd("wood",2);g.Players.RestorePosition(g.ToolUpgrades.Workbench.transform.position+new Vector3(0,.1f,-1.5f));g.OpenCrafting();Phase(2);
                }
                else if(p==2&&View<CraftingView>()!=null)
                {
                    var v=View<CraftingView>();for(int i=0;i<3;i++)v.NextButton.onClick.Invoke();Require(v.PageText.text.Contains("农田用品"),"Crafting supplies have a farming page");v.CraftButtons[0].onClick.Invoke();Require(g.Inventory.GetCount("basic_fertilizer")==5&&g.Inventory.GetCount("wood")==0,"Actual fertilizer recipe costs two wood and yields five");TextCheck(v);Capture(g,"FertilizerCraftingPreview");NoSave(g);g.CloseCrafting();Plot(g,0);g.Tools.SelectTool(FarmTool.Hoe);Require(g.Tools.TryAct(0,out _),"Till via tool");Phase(3);
                }
                else if(p==3&&!g.Tools.Busy) { Require(g.Tools.SelectTool(FarmTool.Seeds)&&g.Tools.TryAct(0,out _)&&g.Farm.State.Cells[0].Quality==CropQuality.Ordinary,"Plant ordinary initial roll");Phase(4); }
                else if(p==4&&!g.Tools.Busy)
                {
                    var hud=View<MagicIsland.Interaction.PrototypeHud>();hud.ToolButtons[6].onClick.Invoke();Require(g.Tools.SelectedTool==FarmTool.Fertilizer,"Seventh AA HUD button selects fertilizer");int stamina=g.Stamina.State.Current;
                    Require(g.Tools.TryAct(0,out _)&&g.Farm.State.Cells[0].Fertilized&&g.Farm.State.Cells[0].Quality==CropQuality.Quality&&g.Inventory.GetCount("basic_fertilizer")==4&&g.Stamina.State.Current==stamina-2,"Actual in-range fertilizer consumes one and two stamina, raises fixed roll");Require(!g.Tools.TryAct(0,out _),"Action lock rejects duplicate input");Phase(5);
                }
                else if(p==5&&!g.Tools.Busy)
                {
                    int stamina=g.Stamina.State.Current;Require(!g.Tools.TryAct(0,out _)&&g.Inventory.GetCount("basic_fertilizer")==4&&g.Stamina.State.Current==stamina,"Same crop cannot be fertilized twice");
                    g.Farm.State.Cells[0].Growth=3;g.Farm.Grid.Refresh();Require(!g.Tools.TryAct(0,out _),"Fertilizer tool cannot harvest mature crop");g.Tools.SelectTool(FarmTool.Hoe);
                    var slots=g.Inventory.State.Slots.ToArray();for(int i=0;i<slots.Length;i++)slots[i]=new InventorySlot("turnip_seed",99);g.Inventory.State.Restore(slots);
                    Require(!g.Tools.TryAct(0,out _)&&g.Farm.State.Cells[0].Mature&&g.Farm.State.Cells[0].Quality==CropQuality.Quality&&g.Farm.State.Cells[0].Fertilized,"Bag-full harvest retains crop, fertilizer and quality");
                    slots[9]=default;g.Inventory.State.Restore(slots);Require(g.Tools.TryAct(0,out _)&&g.Inventory.GetCount("quality_turnip")==1&&g.Inventory.GetCount("turnip")==0&&!g.Farm.State.Cells[0].Fertilized,"Retry harvest receives quality item and consumes cycle fertilizer");NoSave(g);Phase(6);
                }
                else if(p==6&&!g.Tools.Busy)
                {
                    var slots=new InventorySlot[g.Inventory.State.Capacity];g.Inventory.State.Restore(slots);g.Inventory.TryAdd("turnip_seed",10);g.Inventory.TryAdd("basic_fertilizer",3);g.Inventory.TryAdd("quality_turnip",3);g.Inventory.TryAdd("turnip",1);
                    g.Players.RestorePosition(g.Shop.Counter.position+new Vector3(0,.1f,-1.5f));g.OpenShop();Phase(7);
                }
                else if(p==7&&View<ShopView>()!=null)
                {
                    var v=View<ShopView>();v.CropButtons[5].onClick.Invoke();Require(g.Shop.Crop.Id=="quality_turnip"&&!v.BuyButton.interactable&&v.SellButton.interactable,"Shop quality selection sells only");long gold=g.Economy.Gold;v.SellButton.onClick.Invoke();Require(g.Economy.Gold==gold+38&&g.Inventory.GetCount("quality_turnip")==2&&g.Tasks.State.Capture().Single(t=>t.Id=="first_sell").Progress==1,"Actual premium sale pays price and advances crop sale task");TextCheck(v);Capture(g,"QualityCropShopPreview");NoSave(g);g.CloseShop();
                    g.Players.RestorePosition(g.Cooking.Station.transform.position+new Vector3(0,.1f,-1.5f));g.OpenCooking();Phase(8);
                }
                else if(p==8&&View<CookingView>()!=null)
                {
                    int recipe=g.Cooking.Catalog.Recipes.FindIndex(r=>r.FoodId=="turnip_soup");g.CookRecipe(recipe);Require(g.Inventory.GetCount("turnip")==0&&g.Inventory.GetCount("quality_turnip")==1&&g.Inventory.GetCount("turnip_soup")==1,"Actual soup consumes one ordinary before one quality crop");g.CloseCooking();
                    string id=g.Orders.State.ItemId;var crop=g.Configuration.Crops.Crops.First(c=>c.HarvestItemId==id);g.Inventory.TryAdd(crop.QualityHarvestItemId,g.Orders.State.Quantity);g.Players.RestorePosition(g.Orders.Board.transform.position+new Vector3(0,.1f,-1.5f));g.OpenOrder();Phase(9);
                }
                else if(p==9&&View<OrderView>()!=null)
                {
                    string id=g.Orders.State.ItemId;string quality=g.Configuration.Crops.Crops.First(c=>c.HarvestItemId==id).QualityHarvestItemId;long before=g.Inventory.GetCount(quality),gold=g.Economy.Gold,reward=g.Orders.State.Reward;g.DeliverOrder();Require(g.Orders.State.Fulfilled&&g.Inventory.GetCount(quality)==before-g.Orders.State.Quantity&&g.Economy.Gold==gold+reward,"Order accepts quality equivalent and pays original reward once");g.DeliverOrder();Require(g.Economy.Gold==gold+reward,"No duplicate order payout");NoSave(g);g.CloseOrder();
                    g.Inventory.TryAdd("quality_turnip",2);g.Inventory.TryAdd("turnip",2);g.Players.RestorePosition(g.Shipping.Box.transform.position+new Vector3(0,.1f,-1.5f));g.OpenShipping();Phase(10);
                }
                else if(p==10&&View<ShippingView>()!=null)
                {
                    int quality=Enumerable.Range(0,g.Inventory.State.Capacity).First(i=>g.Inventory.State.Slots[i].ItemId=="quality_turnip"),ordinary=Enumerable.Range(0,g.Inventory.State.Capacity).First(i=>g.Inventory.State.Slots[i].ItemId=="turnip");g.TransferShipping(true,quality,2);g.TransferShipping(true,ordinary,2);Require(g.Shipping.State.ExpectedIncome==126&&g.Shipping.State.Quote().Length==2,"Shipping preserves separate quality stacks and prices");NoSave(g);g.CloseShipping();
                    Plot(g,1);g.Tools.SelectTool(FarmTool.Hoe);Require(g.Tools.TryAct(1,out _),"Prepare saved fertilizer plot");Phase(11);
                }
                else if(p==11&&!g.Tools.Busy) { g.Tools.SelectTool(FarmTool.Fertilizer);Require(g.Tools.TryAct(1,out _)&&g.Farm.State.Cells[1].Fertilized,"Can fertilize empty tilled plot");Phase(12); }
                else if(p==12&&!g.Tools.Busy) { g.Tools.SelectTool(FarmTool.Seeds);Require(g.Tools.TryAct(1,out _),"Plant fertilized saved crop");Phase(13); }
                else if(p==13&&!g.Tools.Busy) { SessionState.SetInt(Key+".Quality",(int)g.Farm.State.Cells[1].Quality);SessionState.SetInt(Key+".Seed",g.Farm.State.QualitySeed);SessionState.SetString(Key+".Gold",g.Economy.Gold.ToString());Home(g);Phase(14); }
                else if(p==14&&g.State==GameFlowState.Playing&&g.Housing.Inside) { Rest(g);Phase(15); }
                else if(p==15&&View<SleepView>()!=null)
                {
                    View<SleepView>().ConfirmButton.onClick.Invoke();Require(g.Farm.State.Day==2&&g.Saves.TryLoad(out var saved)&&saved.Version==27&&saved.CropQualitySeed==g.Farm.State.QualitySeed&&saved.Cells[1].Fertilized&&(int)saved.Cells[1].Quality==SessionState.GetInt(Key+".Quality",-1),"Sleep persists fixed field quality and fertilizer in version 27");
                    Require(g.Economy.Gold==long.Parse(SessionState.GetString(Key+".Gold",""))+126&&g.DailyReports.State.Last.Shipping.Length==2,"Sleep pays quality shipments and reports both lines once");SessionState.SetString(Key+".Saved",File.ReadAllText(g.Saves.SavePath));g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(16);
                }
                else if(p==16&&View<PauseView>()!=null) { View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(17); }
                else if(p==17&&g.State==GameFlowState.MainMenu) { g.ContinueGame();Phase(18); }
                else if(p==18&&g.State==GameFlowState.Playing)
                {
                    Require(g.Farm.State.QualitySeed==SessionState.GetInt(Key+".Seed",0)&&g.Farm.State.Cells[1].Fertilized&&(int)g.Farm.State.Cells[1].Quality==SessionState.GetInt(Key+".Quality",-1)&&g.Shipping.State.ExpectedIncome==0,"Reload restores fixed quality and paid empty shipping box");
                    Require(g.Saves.TryLoad(out var current),"Read migration and corruption fixtures");foreach(Action<SaveData> mutate in new Action<SaveData>[] {s=>s.CropQualitySeed=0,s=>s.Cells[1].Quality=(CropQuality)9,s=>s.Cells[1].PlantingNumber=-1,s=> {s.Cells[2].Fertilized=true;s.Cells[2].Tilled=false;},s=>s.Cells[2].Quality=CropQuality.Quality})
                    { var bad=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(current));mutate(bad);Write(g,bad);Require(!g.Saves.TryLoad(out _),"Invalid crop quality metadata rejected"); }
                    current.Version=26;Write(g,current);Require(g.Saves.TryLoad(out var legacy)&&legacy.Version==27&&legacy.CropQualitySeed==1&&legacy.Cells.All(c=>!c.Fertilized&&c.Quality==CropQuality.Ordinary)&&legacy.Gold==current.Gold,"Legacy migration preserves world and gold without retroactive quality");Write(g,JsonUtility.FromJson<SaveData>(SessionState.GetString(Key+".Saved","")));g.ReturnToMainMenu();Phase(19);
                }
                else if(p==19&&View<PauseView>()!=null) { View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(20); }
                else if(p==20&&g.State==GameFlowState.MainMenu)
                { File.WriteAllText("Logs/FertilizerVerification.txt","PASS: packed AA fertilizer config, tool and crafting UI, costs and one-use, deterministic quality, failed harvest preservation, premium shop/task, mixed cooking/order, distinct shipping prices/report, sleep-only version 27 save/reload, corruption and v26 migration, resource cleanup.\n");Debug.Log("Fertilizer flow verification PASSED.");Phase(21);EditorApplication.isPlaying=false; }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        static void Write(GameManager g,SaveData data) { string json=JsonUtility.ToJson(data,true);File.WriteAllText(g.Saves.SavePath,json);File.WriteAllText(g.Saves.SavePath+".bak",json); }
    }
}
