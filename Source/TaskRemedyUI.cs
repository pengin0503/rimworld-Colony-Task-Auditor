using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class Window_TaskRemedy : Window
    {
        readonly Map map;readonly string id;
        TaskDetails detail;
        Vector2 scroll;float height=1000;
        public Window_TaskRemedy(Map map,string id)
        {
            this.map=map;this.id=id;doCloseX=true;draggable=true;resizeable=true;
            absorbInputAroundWindow=false;preventCameraMotion=false;forcePause=false;
            Refresh();
        }
        void Refresh(){var a=map.GetComponent<TaskAuditComponent>();a.Scan();detail=TaskDetails.Inspect(a,id);}
        public override Vector2 InitialSize {get{return new Vector2(700,600);}}
        public override void DoWindowContents(Rect rect)
        {
            var audit=map.GetComponent<TaskAuditComponent>();
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
                list.Label(remedy.Hint.Translate());
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
            var actions=map.GetComponent<TaskActionState>();var watch=actions.FindWatch(id);
            if(list.ButtonText((watch==null?"CTA_WatchAdd":"CTA_WatchRemove").Translate()))actions.Toggle(id);
            watch=actions.FindWatch(id);
            if(watch==null)return;
            list.Label("CTA_WatchThreshold".Translate((watch.Threshold/60000f).ToString("0.2")));
            int changed=(int)list.Slider(watch.Threshold,250,60000);
            if(changed!=watch.Threshold){watch.Threshold=changed;actions.Revision++;}
        }
    }
}
