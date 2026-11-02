using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
        PriorityWrites();
    }
    // #28: natural vanilla ends keep Auto permission; manual/other-mod/unknown writes do not.
    static void PriorityWrites()
    {
        int n=0;Action<bool,string> check=(v,name)=>{if(!v)throw new Exception(name);n++;};
        var none=PriorityWriteSource.Owned;
        check(TemporaryWorkPolicy.ClassifyWrite(true,false,false,none)==PriorityWriteSource.Owned,"own write ignored");
        // Before #28 every one of these withdrew Auto permission like an External write.
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,true,none)==PriorityWriteSource.Timeout,"vanilla timeout Clear is natural");
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,false,PriorityWriteSource.NoWork)==PriorityWriteSource.NoWork,"JobGiver_Work completion Clear is natural");
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,false,PriorityWriteSource.Reset)==PriorityWriteSource.Reset,"mind state reset is engine");
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,false,PriorityWriteSource.Drafted)==PriorityWriteSource.Drafted,"draft is an interruption");
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,false,PriorityWriteSource.TargetChanged)==PriorityWriteSource.TargetChanged,"target removal is engine");
        foreach(var natural in new[]{PriorityWriteSource.Timeout,PriorityWriteSource.NoWork,PriorityWriteSource.Reset,PriorityWriteSource.Drafted,PriorityWriteSource.TargetChanged})
            check(TemporaryWorkPolicy.KeepsAutoPermission(natural),"keeps permission: "+natural);
        // Manual gizmo Clear, other-mod Clear or any unidentified caller: safe side.
        check(TemporaryWorkPolicy.ClassifyWrite(false,false,false,none)==PriorityWriteSource.External,"unidentified Clear is external");
        check(!TemporaryWorkPolicy.KeepsAutoPermission(PriorityWriteSource.External),"external write withdraws permission");
        // Any non-owned Set is a new order, even same-tick, same-value, or inside an engine caller.
        foreach(var ctx in new[]{none,PriorityWriteSource.NoWork,PriorityWriteSource.Reset,PriorityWriteSource.Drafted})
            check(TemporaryWorkPolicy.ClassifyWrite(false,true,true,ctx)==PriorityWriteSource.External,"manual Set wins in "+ctx);
        string root=AppContext.BaseDirectory;
        while(root!=null&&!Directory.Exists(Path.Combine(root,"Languages")))root=Path.GetDirectoryName(root);
        if(root==null)root=Directory.GetCurrentDirectory();
        foreach(string language in new[]{"English","Japanese"})
        {
            var keys=XDocument.Load(Path.Combine(root,"Languages",language,"Keyed","Strings.xml")).Root.Elements().Select(e=>e.Name.LocalName).ToHashSet();
            foreach(PriorityWriteSource s in Enum.GetValues(typeof(PriorityWriteSource)))
                if(s!=PriorityWriteSource.Owned)check(keys.Contains(TemporaryWorkPolicy.HistoryKey(s)),language+" "+s);
        }
        Console.WriteLine($"PASS {n} priority write attribution assertions (#28)");
    }
}
