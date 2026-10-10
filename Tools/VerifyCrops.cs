using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Saving;
using MagicIsland.Stamina;
internal static class VerifyCrops
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected=false; try { action(); } catch (ArgumentException) { rejected=true; } catch (InvalidOperationException) { rejected=true; } Check(rejected,"Invalid crop data rejected"); }
    public static int Main()
    {
        var defs = new[] { new CropDefinition("turnip","seed","crop",3), new CropDefinition("tomato","tomato_seed","tomato",5), new CropDefinition("corn","corn_seed","corn",7) };
        var items = new[] { new ItemDefinition("seed","Seed",ItemType.Seed,99), new ItemDefinition("crop","Crop",ItemType.Crop,99),
            new ItemDefinition("tomato_seed","Tomato seed",ItemType.Seed,99), new ItemDefinition("tomato","Tomato",ItemType.Crop,99),
            new ItemDefinition("corn_seed","Corn seed",ItemType.Seed,99), new ItemDefinition("corn","Corn",ItemType.Crop,99) };
        var farm = new FarmState(2,3,defs); var bag = new InventoryState(6,items); var stamina = new StaminaState(100);
        foreach (var crop in defs) bag.TryAdd(crop.SeedId,2);
        var actions = new FarmActions(farm,bag,"seed","crop",stamina,3,1,2,0,defs);
        Check(!actions.SelectSeed("missing") && actions.SelectedCrop.Id=="turnip","Unknown selection leaves current seed");
        for (int i=0;i<3;i++)
        {
            Check(actions.SelectSeed(defs[i].SeedId),"Select each seed");
            actions.TryAct(i,FarmTool.Hoe,out _); Check(actions.TryAct(i,FarmTool.Seeds,out _),"Plant selected seed");
            Check(farm.Cells[i].CropId==defs[i].Id && farm.Cells[i].RequiredGrowthDays==defs[i].GrowthDays && bag.GetCount(defs[i].SeedId)==1,"Correct crop/seed/growth mapping");
        }
        for(int day=1;day<=7;day++)
        {
            for(int i=0;i<3;i++) if(!farm.Cells[i].Mature) farm.TryAct(i,FarmTool.WateringCan,out _);
            farm.AdvanceDay(); stamina.Refill();
            for(int i=0;i<3;i++) Check(farm.Cells[i].Growth==Math.Min(day,defs[i].GrowthDays) && !farm.Cells[i].Watered,"Independent growth and daily reset");
        }
        var data=SaveData.Capture(farm,bag,0,0,-4,87,900,80,"tomato_seed");
        data.Validate(2,3,new InventoryState(6,items),100,defs);
        var restored=new FarmState(2,3,defs); restored.Restore(data.Day,data.Cells);
        Check(restored.Cells[1].CropId=="tomato" && restored.Cells[2].RequiredGrowthDays==7,"Mixed crop restore");
        data.Cells[2].CropId="unknown"; Reject(()=>data.Validate(2,3,new InventoryState(6,items),100,defs)); Reject(()=>restored.Restore(99,data.Cells));
        Check(restored.Day==farm.Day && restored.Cells[2].CropId=="corn","Restore rejection is atomic"); data.Cells[2].CropId="corn";
        data.Cells[2].RequiredGrowthDays=3; Reject(()=>data.Validate(2,3,new InventoryState(6,items),100,defs)); data.Cells[2].RequiredGrowthDays=7;
        data.SelectedSeedItemId="unknown"; Reject(()=>data.Validate(2,3,new InventoryState(6,items),100,defs)); data.SelectedSeedItemId="tomato_seed";
        actions.SelectSeed("corn_seed");
        for(int i=0;i<3;i++) Check(actions.TryAct(i,FarmTool.Hoe,out _) && bag.GetCount(defs[i].HarvestId)==1 && farm.Cells[i].CropId==null,"Harvest original crop even with a different seed selected");
        Check(stamina.Current==100,"Default harvest is free");
        farm.TryAct(3,FarmTool.Hoe,out _); Check(!farm.TryAct(3,FarmTool.Seeds,out _,"missing") && !farm.Cells[3].Planted,"Unknown planting is atomic");
        var legacyFarm=new FarmState(2); legacyFarm.TryAct(0,FarmTool.Hoe,out _); legacyFarm.TryAct(0,FarmTool.Seeds,out _);
        var legacy=SaveData.Capture(legacyFarm,bag,0,0,-4,87,900,43);
        legacy.Version=4; legacy.Cells[0].CropId=null; legacy.Upgrade(100,100,"turnip","seed");
        Check(legacy.Version==SaveData.CurrentVersion && legacy.Cells[0].CropId=="turnip" && legacy.SelectedSeedItemId=="seed" && legacy.Stamina==43 && legacy.ClockMinute==900 && legacy.Gold==87,"v4 migration preserves progress and assigns turnip");
        legacy.Validate(2,3,new InventoryState(6,items),100,defs);
        Reject(()=>new FarmState(2,3,new[]{defs[0],defs[0]})); Reject(()=>new CropDefinition("bad","seed","crop",0));
        Console.WriteLine("PASS: "+checks+" multi-crop assertions"); return 0;
    }
}
