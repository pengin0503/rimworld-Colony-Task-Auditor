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
            Scribe.saver.InitSaving(path,"fixture");
            record.ExposeData();
            Scribe.saver.FinalizeSaving();
            record=new AuditRecord();
            Scribe.loader.InitLoading(path);
            record.ExposeData();
            Scribe.loader.FinalizeLoading();
            if(record==null || record.Id!="fixture" || record.Clock.EligibleTicks!=90 || !record.Clock.Notified)
                throw new Exception("Scribe record round trip failed");
            record.Clock.Observe(110,5,false,true,100);
            if(!record.Clock.IsStalled(100)||record.Clock.FirstSeen!=10||record.Clock.LastProgress!=10)
                throw new Exception("Scribe progress continuity failed");
            Console.WriteLine("PASS real-game Scribe audit record round trip and progress continuity");
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
