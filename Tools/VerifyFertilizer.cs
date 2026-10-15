using System;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
using MagicIsland.Saving;
class VerifyFertilizer
{
    static int checks;
    static void Check(bool b,string why) { checks++;if(!b)throw new Exception(why); }
    static ItemDefinition[] Items(int max=99)=>new[]{new ItemDefinition("seed","Seed",ItemType.Seed,max),new ItemDefinition("crop","Crop",ItemType.Crop,max),new ItemDefinition("quality_crop","Quality",ItemType.Crop,max),new ItemDefinition("basic_fertilizer","Fertilizer",ItemType.Material,max),new ItemDefinition("food","Food",ItemType.Food,max)};
    static CropDefinition[] Crops()=>new[]{new CropDefinition("turnip","seed","crop",3,"quality_crop")};
    static void Main()
    {
        int normal=0,boosted=0;
        for(int seed=1;seed<=2000;seed++)
        {
            var farm=new FarmState(2,3,Crops(),fertilizer:new FertilizerDefinition(),qualitySeed:seed);var bag=new InventoryState(10,Items());bag.TryAdd("seed",4);bag.TryAdd("basic_fertilizer",4);var stamina=new StaminaState(100);var actions=new FarmActions(farm,bag,"seed","crop",stamina,crops:Crops());var fertilizer=new FertilizerState(farm,bag,stamina);
            Check(fertilizer.Apply(0)==FertilizerResult.Untilled&&bag.GetCount("basic_fertilizer")==4&&stamina.Current==100,"Untilled failure is free");actions.TryAct(0,FarmTool.Hoe,out _);actions.TryAct(0,FarmTool.Seeds,out _);
            int roll=FertilizerDefinition.Roll(seed,0,1);Check((farm.Cells[0].Quality==CropQuality.Quality)==(roll<10),"Normal chance is deterministic per planting");if(roll<10)normal++;
            int stock=(int)bag.GetCount("basic_fertilizer"),energy=stamina.Current;
            Action observer=()=>{Check(farm.Cells[0].Fertilized&&bag.GetCount("basic_fertilizer")==stock-1&&stamina.Current==energy-2,"Observers see fully committed plot, quantity and stamina");Check(fertilizer.Apply(0)==FertilizerResult.Busy,"Observer cannot reapply");};bag.Changed+=observer;
            Check(fertilizer.Apply(0)==FertilizerResult.Success,"Growing crop can be fertilized");bag.Changed-=observer;
            Check((farm.Cells[0].Quality==CropQuality.Quality)==(roll<40),"Fertilizer raises chance using same roll");if(roll<40)boosted++;
            Check(fertilizer.Apply(0)==FertilizerResult.AlreadyApplied&&bag.GetCount("basic_fertilizer")==stock-1&&stamina.Current==energy-2,"One fertilizer per crop cycle");
            var saved=SaveData.Capture(farm,bag,0,0,0);saved.SelectedSeedItemId="seed";saved.Validate(2,3,bag.CreateEmpty(10),crops:Crops(),defaultCropId:"turnip");
            var loaded=new FarmState(2,3,Crops(),qualitySeed:saved.CropQualitySeed);loaded.Restore(saved.Day,saved.Cells);Check(loaded.Cells[0].Quality==farm.Cells[0].Quality&&loaded.Cells[0].Fertilized&&loaded.Cells[0].PlantingNumber==1,"Quality and fertilizer survive exact restore");
            saved.Cells[0].Quality=(CropQuality)9;bool rejected=false;try{saved.Validate(2,3,bag.CreateEmpty(10),crops:Crops());}catch(Exception){rejected=true;}Check(rejected,"Unknown quality rejected");
            farm.Cells[0].Growth=3;Check(fertilizer.Apply(0)==FertilizerResult.Mature,"Mature crop cannot be fertilized");string harvested=farm.HarvestId(0);long before=bag.GetCount(harvested);
            Check(actions.TryAct(0,FarmTool.Hoe,out _)&&bag.GetCount(harvested)==before+1&&!farm.Cells[0].Fertilized&&farm.Cells[0].Quality==CropQuality.Ordinary&&farm.Cells[0].PlantingNumber==1,"Harvest gives exact quality and consumes cycle fertilizer");
            Check(fertilizer.Apply(0)==FertilizerResult.Success&&actions.TryAct(0,FarmTool.Seeds,out _)&&farm.Cells[0].PlantingNumber==2,"Fertilizer on empty tilled ground carries into next planting");
            var legacy=SaveData.Capture(farm,bag,0,0,0);legacy.Version=26;legacy.Upgrade(100);Check(legacy.Version==SaveData.CurrentVersion&&legacy.CropQualitySeed==1&&!legacy.Cells[0].Fertilized&&legacy.Cells[0].Quality==CropQuality.Ordinary&&legacy.Cells[0].PlantingNumber==1,"Version 26 migration preserves old crops as ordinary without retroactive fertilizer");
        }
        Check(normal>130&&normal<270&&boosted>650&&boosted<950&&boosted>normal,"Sampling matches intended normal and fertilized rates");
        var f=new FarmState(1,3,Crops(),fertilizer:new FertilizerDefinition(normalChance:0,fertilizedChance:100));var b=new InventoryState(1,Items());var s=new StaminaState(100);var action=new FarmActions(f,b,"seed","crop",s,crops:Crops());
        f.TryAct(0,FarmTool.Hoe,out _);f.TryAct(0,FarmTool.Seeds,out _);b.TryAdd("basic_fertilizer",1);var fs=new FertilizerState(f,b,s);s.Restore(1);Check(fs.Apply(0)==FertilizerResult.Exhausted&&!f.Cells[0].Fertilized&&b.GetCount("basic_fertilizer")==1,"Low stamina does not consume fertilizer");s.Restore(100);fs.Apply(0);f.Cells[0].Growth=3;b.TryAdd("seed",99);
        Check(!action.TryAct(0,FarmTool.Hoe,out _)&&f.Cells[0].Mature&&f.Cells[0].Fertilized&&f.Cells[0].Quality==CropQuality.Quality,"Full backpack retains fixed quality crop");b.TryRemove("seed",99);Check(action.TryAct(0,FarmTool.Hoe,out _)&&b.GetCount("quality_crop")==1,"Retry harvest does not reroll");
        var resolver=new IngredientResolver(new[]{new KeyValuePair<string,string>("crop","quality_crop"),new KeyValuePair<string,string>("egg","quality_egg")});var bag2=new InventoryState(10,Items());bag2.TryAdd("crop",1);bag2.TryAdd("quality_crop",3);
        Check(resolver.Resolve(bag2,new[]{new IngredientCost("crop",2)},out var costs)&&costs.Single(c=>c.Id=="crop").Quantity==1&&costs.Single(c=>c.Id=="quality_crop").Quantity==1,"Ordinary stock used before quality stock");
        Check(resolver.Resolve(bag2,new[]{new IngredientCost("crop",3),new IngredientCost("quality_crop",1)},out costs)&&costs.Single(c=>c.Id=="quality_crop").Quantity==3,"Explicit quality demand reserved before fallback");
        Check(!resolver.Resolve(bag2,new[]{new IngredientCost("crop",4),new IngredientCost("quality_crop",1)},out _)&&bag2.GetCount("quality_crop")==3,"Insufficient mixed ingredients leave all quantities unchanged");
        Console.WriteLine("PASS: "+checks+" fertilizer, deterministic quality, transaction, harvest, ingredient and migration assertions");
    }
}
