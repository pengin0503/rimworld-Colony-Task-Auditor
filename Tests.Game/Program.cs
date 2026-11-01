using System;
using System.IO;
using Pengin.ColonyTaskAuditor;
using Verse;

class Program
{
    static void Main()
    {
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
            Console.WriteLine("PASS real-game Scribe audit record round trip");
        }
        finally {if(File.Exists(path))File.Delete(path);}
    }
}
