using System;
using Pengin.ColonyTaskAuditor;
static class AssistanceTests
{
    static AssistanceFacts Safe(){return new AssistanceFacts{Complete=true,Current=true,VerifiedJob=true,Idle=true,Safe=true,Enabled=true,Vanilla=true};}
    public static void Run()
    {
        if(!TemporaryWorkPolicy.FreshScan(101,100)||!TemporaryWorkPolicy.FreshScan(350,100)||TemporaryWorkPolicy.FreshScan(351,100)||TemporaryWorkPolicy.FreshScan(99,100))throw new Exception("component order and scan freshness");
        var f=Safe();
        if(!TemporaryWorkPolicy.CanStart(f,ActionMode.Confirm,false,false,false,60000,0,60000,0,2))throw new Exception("confirmed safe support");
        foreach(string field in new[]{"Complete","Current","VerifiedJob","Idle","Safe","Enabled","Vanilla"})
        {
            f=Safe();typeof(AssistanceFacts).GetField(field).SetValue(f,false);
            if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,true,true,60000,0,60000,0,2))throw new Exception("unsafe "+field);
        }
        f=Safe();f.PriorityConflict=true;
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,true,true,60000,0,60000,0,2))throw new Exception("Work Tab conflict");
        f=Safe();f.HasManualPriority=true;
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Confirm,false,true,true,60000,0,60000,0,2))throw new Exception("manual request wins");
        f=Safe();
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,false,true,60000,0,60000,0,2))throw new Exception("watch required");
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,true,false,60000,0,60000,0,2))throw new Exception("permission required");
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,true,true,59999,0,60000,0,2))throw new Exception("cooldown");
        if(TemporaryWorkPolicy.CanStart(f,ActionMode.Auto,true,true,true,60000,0,60000,2,2))throw new Exception("concurrent cap");
        if(TemporaryWorkPolicy.ClearOwned(20,"mine",new[]{1,2},21,"mine",new[]{1,2}))throw new Exception("manual replacement timestamp preserved");
        if(TemporaryWorkPolicy.ClearOwned(20,"mine",new[]{1,2},20,"grow",new[]{1,2}))throw new Exception("other giver preserved");
        if(!TemporaryWorkPolicy.ClearOwned(20,"mine",new[]{1,2},20,"mine",new[]{1,2}))throw new Exception("owned request cleaned up");
        Console.WriteLine("PASS 18 temporary work safety scenarios");
    }
}
