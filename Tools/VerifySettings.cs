using System;
using System.IO;
using MagicIsland.Settings;
using MagicIsland.Saving;
class VerifySettings
{
    static int checks;
    static void Check(bool b,string why) { checks++; if(!b)throw new Exception(why); }
    static void Main()
    {
        foreach(bool fullscreen in new[]{false,true}) for(int i=0;i<=100;i++)Check(SettingsState.Valid(new SavedSettings{Volume=i/100f,Fullscreen=fullscreen}),"Valid settings ranges");
        foreach(float value in new[]{-1f,1.01f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})Check(!SettingsState.Valid(new SavedSettings{Volume=value}),"Invalid volume rejected");
        Check(!SettingsState.Valid(null)&&!SettingsState.Valid(new SavedSettings{Version=0})&&!SettingsState.Valid(new SavedSettings{Version=2}),"Invalid settings version rejected");
        Check(SettingsState.Clamp(-1)==0&&SettingsState.Clamp(2)==1&&SettingsState.Clamp(float.NaN)==1&&SettingsState.Clamp(float.PositiveInfinity)==1,"Input clamping is deterministic");
        var dir=Path.GetFullPath("Logs/SettingsRules/"+Guid.NewGuid().ToString("N"));var game=new SaveFileStore(dir);var prefs=new SaveFileStore(dir,"settings.json");
        game.Write("game checkpoint");prefs.Write("settings1");prefs.Write("settings2");Check(File.ReadAllText(game.FilePath)=="game checkpoint"&&!File.Exists(game.BackupPath),"Settings writes cannot replace game file or its backup");
        Check(File.ReadAllText(prefs.FilePath)=="settings2"&&File.ReadAllText(prefs.BackupPath)=="settings1","Atomic settings backup maintained");
        File.WriteAllText(prefs.FilePath,"invalid");Check(prefs.TryRead(s=>{if(!s.StartsWith("settings"))throw new Exception();return s;},out var data,out _,out bool recovered)&&recovered&&data=="settings1","Invalid settings recover independent backup");
        Console.WriteLine("PASS: "+checks+" setting validation, input, independent persistence and backup assertions");
    }
}
