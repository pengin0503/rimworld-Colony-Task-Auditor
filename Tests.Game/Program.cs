using System;
using System.IO;
using Pengin.ColonyTaskAuditor;
using Verse;

class Program
{
    static void Main()
    {
        // Report parser failures directly before the game logger can mask their cause.
        try { _=ParseHelper.FromString<string>("fixture"); }
        catch(Exception e) { Console.Error.WriteLine("Game parser preflight failed: "+e); throw; }
        // This record-only fixture has no engine startup. Supply the real game's default
        // PrefsData so DeepProfiler can read LogVerbose during load finalization.
        // This does not initialize a playable game or validate a whole-map save.
        typeof(Prefs).GetField("data",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
            .SetValue(null,new PrefsData());
        // Private integration test. Uses the real game's Scribe, not a mock serializer.
        // This is a record round trip, not a playable-map save/load acceptance test.
        string path=Path.Combine(Path.GetTempPath(),"cta-"+Guid.NewGuid()+".xml");
        try
        {
            var record=new AuditRecord {Id="fixture",Category="bill",FirstSeen=10,LastSeen=100,X=1,Z=2};
            record.Clock.Observe(10,5,false,true,100);
            record.Clock.Observe(100,5,false,true,100);
            record.Clock.Notified=true;
            var watch=new TaskWatchEntry{Info=new WatchInfo{Id="bill:fixture",MapId=7,Threshold=6000,AutoAssist=true}};
            var settings=new AuditorSettings{RemedyMode=ActionMode.Auto,AssistanceDuration=5000};
            Scribe.saver.InitSaving(path,"fixture");
            Scribe_Deep.Look(ref watch,"watch");Scribe_Deep.Look(ref settings,"settings");
            record.ExposeData();
            Scribe.saver.FinalizeSaving();
            record=new AuditRecord();watch=null;settings=null;
            Scribe.loader.InitLoading(path);
            record.ExposeData();
            // Same LoadingVars pass as Scribe_Deep, without ScribeExtractor's BackCompatibility
            // type lookup, which needs a running game.
            watch=new TaskWatchEntry();Scribe.EnterNode("watch");try{watch.ExposeData();}finally{Scribe.ExitNode();}
            settings=new AuditorSettings();Scribe.EnterNode("settings");try{settings.ExposeData();}finally{Scribe.ExitNode();}
            Scribe.loader.FinalizeLoading();
            if(watch.Info.Id!="bill:fixture"||watch.Info.MapId!=7||watch.Info.Threshold!=6000||!watch.Info.AutoAssist||settings.RemedyMode!=ActionMode.Auto||settings.AssistanceDuration!=5000)
                throw new Exception("Scribe watch/settings round trip failed");
            if(record==null || record.Id!="fixture" || record.Clock.EligibleTicks!=90 || !record.Clock.Notified)
                throw new Exception("Scribe record round trip failed");
            record.Clock.Observe(110,5,false,true,100);
            if(!record.Clock.IsStalled(100)||record.Clock.FirstSeen!=10||record.Clock.LastProgress!=10)
                throw new Exception("Scribe progress continuity failed");
            Console.WriteLine("PASS real-game Scribe audit record round trip and progress continuity");
            // #28: the vanilla members the priority-write attribution binds to.
            var nonPublic=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var timeout=typeof(PriorityWork).GetField("Timeout",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            if(timeout==null||!timeout.IsLiteral||(int)timeout.GetRawConstantValue()!=30000)throw new Exception("PriorityWork timeout changed");
            foreach(string field in new[]{"pawn","prioritizedCell","prioritizeTick"})
                if(typeof(PriorityWork).GetField(field,nonPublic)==null)throw new Exception("PriorityWork field missing: "+field);
            if(typeof(PriorityWork).GetProperty("IsPrioritized")?.GetGetMethod()==null||
                typeof(RimWorld.JobGiver_Work).GetMethod("TryIssueJobPackage",new[]{typeof(Pawn),typeof(Verse.AI.JobIssueParams)})==null||
                typeof(Verse.AI.Pawn_MindState).GetMethod("Reset",new[]{typeof(bool),typeof(bool),typeof(bool)})==null||
                typeof(RimWorld.Pawn_DraftController).GetProperty("Drafted")?.GetSetMethod()==null||
                typeof(Verse.AI.ThingCountTracker).GetMethod("InterruptEnroutePawns",new[]{typeof(Pawn)})==null)
                throw new Exception("vanilla priority-end caller changed");
            Console.WriteLine("PASS real-game PriorityWork API and vanilla priority-end callers match the attribution hooks");
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
