using System;
using Pengin.ColonyTaskAuditor;

static class Program
{
    static int tests;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); tests++; }
    static void Main()
    {
        Check(BlockerEvidence.Materials(5,0)=="CTA_MaterialMissing","missing delivered materials are diagnosed");
        Check(BlockerEvidence.Materials(5,5)=="CTA_MaterialDelivery","available material still needs delivery");
        Check(BlockerEvidence.Materials(0,0)==null,"completed delivery is not a shortage");
        Check(BlockerEvidence.Reservation(true,true)==null,"reservation alone is not blockage");
        Check(BlockerEvidence.Reservation(true,false)=="CTA_Reserved","reservation plus rejected worker reservation is evidence");
        var c = new ProgressClock();
        c.Observe(100, 0, false, true, 20);
        Check(!c.IsStalled(10), "fresh task has grace");
        c.Observe(110, 0, false, true, 20);
        Check(c.IsStalled(10), "eligible unchanged work ages");
        c.Notified = true;
        c.Observe(120, 1, false, true, 20);
        Check(!c.IsStalled(10) && c.LastProgress == 120 && !c.Notified, "actual progress resets notification episode");
        c.Observe(130, 1, true, true, 20);
        Check(c.Started == 130 && !c.IsStalled(1), "active job is not a stall");
        c.Observe(140, 1, false, false, 20);
        c.Observe(150, 1, false, false, 20);
        c.Observe(160, 1, false, true, 20);
        Check(!c.IsStalled(1), "paused and resume interval do not age work");
        c.Observe(170, 1, false, true, 20);
        Check(c.IsStalled(10), "resume starts new observation interval");
        c.Observe(999, 1, false, true, 20);
        Check(!c.IsStalled(1), "missed scans reset uncertain elapsed time");
        c.Observe(2, 1, false, true, 20);
        Check(!c.IsStalled(1) && c.FirstSeen == 2, "rollback resets future state");
        var legacy = new ProgressClock { FirstSeen=1, LastSeen=1000000 };
        legacy.Observe(1000010, 0, false, true, 20);
        Check(!legacy.IsStalled(1), "legacy record must acquire progress baseline");
        var saved = new ProgressClock { Initialized=c.Initialized,FirstSeen=c.FirstSeen,LastSeen=c.LastSeen,LastProgress=c.LastProgress,Started=c.Started,Progress=c.Progress,EligibleTicks=c.EligibleTicks,WasEligible=c.WasEligible,Notified=true };
        saved.Observe(12,1,false,true,20);
        Check(saved.IsStalled(10) && saved.Notified, "persisted primitive state preserves episode");
        saved.MarkProgress(13);
        Check(saved.LastProgress==13&&!saved.Notified&&!saved.IsStalled(1),"completed work resets stall even without numeric change");
        var bad = new ProgressClock();
        bad.Observe(0,float.NaN,false,true,20);
        bad.Observe(10,float.NaN,false,true,20);
        Check(!bad.IsStalled(1), "non-finite observation is ineligible");
        var timer=System.Diagnostics.Stopwatch.StartNew();
        var many=new ProgressClock[2000];
        for(int i=0;i<many.Length;i++) many[i]=new ProgressClock();
        for(int tick=0;tick<1000;tick++) foreach(var item in many) item.Observe(tick,0,false,true,20);
        timer.Stop();
        Check(many[1999].EligibleTicks==999,"large repeated clock set remains consistent");
        Console.WriteLine($"PASS {tests} progress assertions; 2000 clocks x 1000 observations: {timer.Elapsed.TotalMilliseconds:F2} ms (model only, not game TPS)");
    }
}
