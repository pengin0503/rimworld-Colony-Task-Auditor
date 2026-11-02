using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class Window_TaskRemedy : Window
    {
        readonly Map map;readonly string id;
        int detailScan=-1;
        TaskDetails detail;
        WorkAssistancePreview assistance;
        Vector2 scroll;float height=1000;
        public Window_TaskRemedy(Map map,string id)
        {
            this.map=map;this.id=id;doCloseX=true;draggable=true;resizeable=true;
            absorbInputAroundWindow=false;preventCameraMotion=false;forcePause=false;
            Refresh();
        }
        void Refresh(){var a=map.GetComponent<TaskAuditComponent>();a.Scan();detail=TaskDetails.Inspect(a,id);detailScan=a.LastScanTick;}
        public override Vector2 InitialSize {get{return new Vector2(700,600);}}
        public override void DoWindowContents(Rect rect)
        {
            var audit=map.GetComponent<TaskAuditComponent>();
            if(detailScan!=audit.LastScanTick)
            {detail=TaskDetails.Inspect(audit,id);detailScan=audit.LastScanTick;assistance=null;}
            Rect view=new Rect(rect.x,rect.y,rect.width,rect.height-30);
            Rect content=new Rect(0,0,rect.width-20,System.Math.Max(height,view.height));
            Widgets.BeginScrollView(view,ref scroll,content);var list=new Listing_Standard();list.Begin(content);
            if(list.ButtonText("CTA_RefreshDetails".Translate()))Refresh();
            if(detail==null||!detail.Current){list.Label("CTA_TargetGone".Translate());}
            else
            {
                var r=detail.Record;
                var remedy=TaskRemedyPlanner.Plan(r.Reason,r.Confidence=="CTA_Confirmed",audit.PartialScan,false,r.Held,false);
                list.Label(r.Description);list.Label(r.Reason.Translate()+" / "+r.Confidence.Translate());
                if(TaskAuditorMod.Settings.RemedyMode!=ActionMode.Off)list.Label(remedy.Hint.Translate());
                list.Label("CTA_ActionEvidence".Translate());
                if(audit.PartialScan)list.Label("CTA_Partial".Translate());
                list.Label("CTA_MaterialAssumptions".Translate());
                foreach(var line in detail.Materials)list.Label(line);
                if(detail.Recipes.Count>0)list.Label("CTA_RecipeAdvice".Translate());
                foreach(var line in detail.Recipes)list.Label(line);
                list.Label("CTA_WorkerDiagnostics".Translate(detail.Work==null?"?":detail.Work.LabelCap.ToString(),detail.Context.MinSkill));
                foreach(var candidate in detail.Workers)list.Label(candidate.Pawn.LabelShort+": "+candidate.Status.Translate());
                DrawActions(list,audit);
            }
            height=list.CurHeight+20;list.End();Widgets.EndScrollView();
        }
        void DrawActions(Listing_Standard list,TaskAuditComponent audit)
        {
            // Cause-specific guidance only navigates to live objects. Existing
            // confirmation and scanner proofs still govern every world write.
            var navigation=TaskRemedyPlanner.NavigationFor(detail.Record.Reason);
            if(navigation==RemedyNavigation.Materials)
            {
                var stock=detail.MaterialTargets.FirstOrDefault(t=>t!=null&&t.Spawned&&t.Map==map);
                if(stock!=null&&list.ButtonText("CTA_JumpMaterial".Translate(stock.LabelShort)))
                {CameraJumper.TryJump(stock.Position,map);Find.Selector.ClearSelection();Find.Selector.Select(stock);}
                var bench=detail.RecipeTargets.FirstOrDefault(t=>t!=null&&t.Spawned&&t.Map==map);
                if(bench!=null&&list.ButtonText("CTA_JumpRecipeBench".Translate(bench.LabelShort)))
                {CameraJumper.TryJump(bench.Position,map);Find.Selector.ClearSelection();Find.Selector.Select(bench);}
            }
            else if(navigation==RemedyNavigation.Worker)
            {
                foreach(var worker in detail.Workers.Where(w=>w.Pawn!=null&&w.Pawn.Spawned&&w.Pawn.Map==map).Take(3))
                    if(list.ButtonText("CTA_JumpWorker".Translate(worker.Pawn.LabelShort,worker.Status.Translate())))
                    {CameraJumper.TryJump(worker.Pawn.Position,map);Find.Selector.ClearSelection();Find.Selector.Select(worker.Pawn);}
            }
            // Explicit manual override for a verified paused bill. No automatic
            // unpausing and no write to the work priority system.
            if(TaskAuditorMod.Settings.RemedyMode!=ActionMode.Off&&detail.Context.Bill!=null&&
                TaskRemedyPlanner.CanResumePausedBill(detail.Record.Reason,true,audit.PartialScan,
                    detail.Context.Bill.suspended,true)&&list.ButtonText("CTA_ResumePausedBill".Translate()))
            {
                var bill=detail.Context.Bill;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CTA_ResumeBillConfirm".Translate(),()=>
                {
                    audit.Scan();
                    var current=TaskDetails.Inspect(audit,id,false,0);
                    if(Find.CurrentMap!=map||!current.Current||current.Context.Bill!=bill||
                        !TaskRemedyPlanner.CanResumePausedBill(current.Record.Reason,true,audit.PartialScan,
                            bill.suspended,true))
                    {Messages.Message("CTA_ActionChanged".Translate(),MessageTypeDefOf.RejectInput,false);return;}
                    bill.suspended=false;assistance=null;Refresh();
                }));
            }
            if(detail.Context.Target!=null&&list.ButtonText("CTA_SelectTarget".Translate()))
            {CameraJumper.TryJump(detail.Context.Target.Position,map);Find.Selector.ClearSelection();Find.Selector.Select(detail.Context.Target);}
            if(list.ButtonText("CTA_OpenWorkTable".Translate()))
            {var tab=DefDatabase<MainButtonDef>.GetNamedSilentFail("Work");if(tab!=null)Find.MainTabsRoot.SetCurrentTab(tab);}
            var actions=map.GetComponent<TaskActionState>();var watch=actions.FindWatch(id);
            if(list.ButtonText((watch==null?"CTA_WatchAdd":"CTA_WatchRemove").Translate()))actions.Toggle(id);
            watch=actions.FindWatch(id);
            if(watch!=null)
            {
            bool allowed=watch.AutoAssist;list.CheckboxLabeled("CTA_AllowAutoTarget".Translate(),ref allowed);
            if(allowed!=watch.AutoAssist){watch.AutoAssist=allowed;actions.Revision++;}
            list.Label("CTA_WatchThreshold".Translate((watch.Threshold/60000f).ToString("0.00")));
            int changed=(int)list.Slider(watch.Threshold,250,60000);
            if(changed!=watch.Threshold){watch.Threshold=changed;actions.Revision++;}
            }
            if(TaskAuditorMod.Settings.AssistanceMode==ActionMode.Off)return;
            if(TemporaryWorkAssistance.PriorityConflict())list.Label("CTA_PriorityConflict".Translate());
            foreach(var candidate in detail.Workers.Where(w=>w.Status=="CTA_WorkerCandidate").Take(8))
                if(list.ButtonText("CTA_PreviewWorker".Translate(candidate.Pawn.LabelShort)))assistance=TemporaryWorkAssistance.Preview(audit,id,candidate.Pawn);
            if(assistance==null)return;
            if(!assistance.Ready){list.Label(assistance.Blocked.Translate());return;}
            list.Label("CTA_AssistancePreview".Translate(assistance.Pawn.LabelShort,assistance.Giver.LabelCap,(TaskAuditorMod.Settings.AssistanceDuration/2500f).ToString("0.0")));
            if(list.ButtonText("CTA_ConfirmAssistance".Translate()))
            {
                var shown=assistance;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CTA_AssistanceConfirm".Translate(shown.Pawn.LabelShort,shown.Giver.LabelCap),()=>
                {
                    if(!TemporaryWorkAssistance.Execute(audit,shown,false))Messages.Message("CTA_ActionChanged".Translate(),MessageTypeDefOf.RejectInput,false);
                    assistance=null;
                }));
            }
        }
    }
}
