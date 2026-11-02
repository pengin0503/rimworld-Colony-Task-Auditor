using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Pengin.ColonyTaskAuditor
{
    // Shared list drawing for the mod settings page and the in-game window.
    sealed class TaskAuditListView
    {
        static readonly string[] categories = {"all","designation","bill","grow","unfinished","gather"};
        Vector2 scroll;
        int filter;
        bool overdueOnly,importantOnly;
        int cacheRevision=-1;bool cacheImportant;ActionMode cacheMode;
        // Sorting thousands of records every GUI event is wasteful: rebuild only when the
        // underlying scan, filter or threshold changes.
        List<AuditRecord> cache;
        int cacheScan=-1,cacheFilter=-1,cacheThreshold=-1;bool cacheOverdue;
        object cacheSource;

        public void Draw(Rect rect, TaskAuditComponent audit, Map map, bool closeSettingsOnJump)
        {
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, 150, 30), "CTA_Rescan".Translate()))
            {audit.Scan();cacheScan=-1;}
            if (Widgets.ButtonText(new Rect(rect.x+rect.width-150,rect.y,150,30), ("CTA_Filter"+categories[filter]).Translate()))
                filter=(filter+1)%categories.Length;
            Widgets.CheckboxLabeled(new Rect(rect.x+rect.width-330,rect.y+4,170,24),"CTA_OverdueOnly".Translate(),ref overdueOnly);
            var actions=map.GetComponent<TaskActionState>();
            Widgets.CheckboxLabeled(new Rect(rect.x,rect.y+32,180,24),"CTA_ImportantOnly".Translate(),ref importantOnly);
            int threshold = TaskAuditorMod.Settings.WarningDays * 60000;
            if(cache==null||cacheRevision!=actions.Revision||cacheImportant!=importantOnly||cacheMode!=TaskAuditorMod.Settings.AssistanceMode||cacheScan!=audit.LastScanTick||cacheFilter!=filter||cacheThreshold!=threshold||cacheOverdue!=overdueOnly||cacheSource!=audit)
            {
                // Overdue items are sorted first so users see urgent issues without paging.
                cache=audit.Records.Where(r=>(filter==0 || r.Category==categories[filter])&&(!importantOnly||actions.FindWatch(r.Id)!=null)&&(!overdueOnly||r.Clock.ReachedThreshold(audit.Threshold(r))))
                    .OrderBy(r=>actions.FindWatch(r.Id)==null).ThenBy(r => !r.Clock.ReachedThreshold(audit.Threshold(r))).ThenBy(r => r.FirstSeen).ToList();
                cacheRevision=actions.Revision;cacheImportant=importantOnly;cacheMode=TaskAuditorMod.Settings.AssistanceMode;
                cacheScan=audit.LastScanTick;cacheFilter=filter;cacheThreshold=threshold;cacheOverdue=overdueOnly;cacheSource=audit;
            }
            Widgets.Label(new Rect(rect.x + 155, rect.y + 4, Math.Max(50f,rect.width - 490), 28),
                "CTA_Count".Translate(audit.Records.Count, audit.Overdue.Count));
            float footer=audit.PartialScan||audit.FailedItems>0?24f:0f;
            Rect viewport = new Rect(rect.x, rect.y + 64, rect.width, Math.Max(75f, rect.height - 64 - footer));
            const float row=108f;
            Rect content = new Rect(0, 0, Math.Max(100f, viewport.width - 20), Math.Max(viewport.height, cache.Count * row));
            Widgets.BeginScrollView(viewport, ref scroll, content);
            // Only draw the visible window, not every off-screen record.
            int first=Math.Max(0,(int)(scroll.y/row)-1);
            int last=Math.Min(cache.Count,(int)((scroll.y+viewport.height)/row)+2);
            for (int i = first; i < last; i++)
            {
                AuditRecord item = cache[i];
                bool overdue = item.Clock.ReachedThreshold(audit.Threshold(item));
                Widgets.Label(new Rect(0, i * row, content.width - 80f, row-3),
                    (overdue ? "CTA_Overdue".Translate() : "CTA_Watching".Translate()) +
                    " [" + ("CTA_Filter"+item.Category).Translate() + "] " + item.Description + "\n" +
                    "CTA_Days".Translate((item.Clock.EligibleTicks / 60000f).ToString("0.0")).ToString() + " / " + item.Reason.Translate().ToString() + " (" + item.Confidence.Translate() + ")" +
                    (item.Held?" / "+"CTA_HeldBacklog".Translate().ToString():"") + "\n" +
                    "CTA_Timing".Translate(item.FirstSeen,item.Clock.Started<0?"—":item.Clock.Started.ToString(),item.Clock.HasProgress?item.Clock.LastProgress.ToString():"—"));
                if(Widgets.ButtonText(new Rect(content.width-76f,i*row+71,73f,28f),(actions.FindWatch(item.Id)==null?"CTA_WatchAdd":"CTA_WatchRemove").Translate()))
                    actions.Toggle(item.Id);
                if(Widgets.ButtonText(new Rect(content.width-76f,i*row+38,73f,28f),"CTA_Details".Translate()))
                    Find.WindowStack.Add(new Window_TaskRemedy(map,item.Id));
                if (item.X >= 0 && Widgets.ButtonText(new Rect(content.width - 76f, i * row + 5, 73f, 28f), "CTA_Jump".Translate()))
                {
                    // The settings dialog covers the map; close it so the jump is visible.
                    if(closeSettingsOnJump)Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
                    CameraJumper.TryJump(new IntVec3(item.X, 0, item.Z), map);
                }
            }
            Widgets.EndScrollView();
            if (footer>0)
                Widgets.Label(new Rect(rect.x, rect.yMax - 22, rect.width, 22),
                    audit.FailedItems>0?"CTA_PartialFailed".Translate(audit.FailedItems).ToString():"CTA_Partial".Translate().ToString());
        }
    }

    // Non-modal list: the map stays visible and movable while it is open.
    public sealed class Window_TaskAudit : Window
    {
        readonly TaskAuditListView view=new TaskAuditListView();
        public Window_TaskAudit()
        {
            doCloseX=true;draggable=true;resizeable=true;
            absorbInputAroundWindow=false;closeOnClickedOutside=false;preventCameraMotion=false;forcePause=false;
        }
        public override Vector2 InitialSize { get { return new Vector2(760f,560f); } }
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font=GameFont.Medium;
            Widgets.Label(new Rect(inRect.x,inRect.y,inRect.width-40,34),"Colony Task Auditor");
            Text.Font=GameFont.Small;
            Map map=Find.CurrentMap;
            TaskAuditComponent audit=map==null?null:map.GetComponent<TaskAuditComponent>();
            Rect body=new Rect(inRect.x,inRect.y+38,inRect.width,inRect.height-38);
            if(audit==null){Widgets.Label(body,"CTA_NoMap".Translate());return;}
            view.Draw(body,audit,map,false);
        }
    }

    // Standard right-side alert: lists overdue tasks on every player map.
    public sealed class Alert_TaskAuditOverdue : Alert
    {
        readonly List<GlobalTargetInfo> culprits=new List<GlobalTargetInfo>();
        readonly List<AuditRecord> overdue=new List<AuditRecord>();
        public Alert_TaskAuditOverdue(){defaultPriority=AlertPriority.Medium;}
        void Collect()
        {
            culprits.Clear();overdue.Clear();
            if(TaskAuditorMod.Settings==null||!TaskAuditorMod.Settings.ShowAlert)return;
            foreach(Map map in Find.Maps)
            {
                TaskAuditComponent audit=map.GetComponent<TaskAuditComponent>();
                if(audit==null)continue;
                int threshold=TaskAuditorMod.Settings.WarningDays*60000;
                foreach(AuditRecord r in audit.Overdue)
                {
                    // The cache is from the last scan; work may have resumed since.
                    if(!r.Clock.ReachedThreshold(audit.Threshold(r)))continue;
                    overdue.Add(r);
                    if(r.X>=0&&culprits.Count<50)culprits.Add(new GlobalTargetInfo(new IntVec3(r.X,0,r.Z),map));
                }
            }
        }
        public override AlertReport GetReport()
        {
            Collect();
            if(overdue.Count==0)return AlertReport.Inactive;
            return culprits.Count>0?AlertReport.CulpritsAre(culprits):AlertReport.Active;
        }
        public override string GetLabel(){return "CTA_AlertLabel".Translate(overdue.Count);}
        public override TaggedString GetExplanation()
        {
            var lines=overdue.Take(10).Select(r=>"- ["+("CTA_Filter"+r.Category).Translate()+"] "+r.Description+": "+r.Reason.Translate()+" ("+r.Confidence.Translate()+")");
            string more=overdue.Count>10?"\n"+"CTA_AlertMore".Translate(overdue.Count-10).ToString():"";
            return "CTA_AlertExplanation".Translate()+"\n\n"+string.Join("\n",lines)+more+"\n\n"+"CTA_AlertClick".Translate();
        }
        protected override void OnClick()
        {
            // First click opens the list; while it is open, clicks cycle through the locations.
            if(!Find.WindowStack.IsOpen<Window_TaskAudit>())
            {
                // The window lists the current map: switch to a map with overdue tasks first.
                Map current=Find.CurrentMap;
                TaskAuditComponent here=current==null?null:current.GetComponent<TaskAuditComponent>();
                int limit=TaskAuditorMod.Settings.WarningDays*60000;
                if(here==null||!here.Overdue.Any(r=>r.Clock.ReachedThreshold(here.Threshold(r))))
                {
                    Map other=Find.Maps.FirstOrDefault(m=>{var a=m.GetComponent<TaskAuditComponent>();return a!=null&&a.Overdue.Any(r=>r.Clock.ReachedThreshold(a.Threshold(r)));});
                    if(other!=null)CameraJumper.TryJump(other.Center,other);
                }
                Find.WindowStack.Add(new Window_TaskAudit());
                return;
            }
            base.OnClick();
        }
    }

    public sealed class TaskAuditorMod : Mod
    {
        public static AuditorSettings Settings;
        public static TaskAuditorMod Instance;
        readonly TaskAuditListView view=new TaskAuditListView();
        public TaskAuditorMod(ModContentPack pack) : base(pack)
        {
            Instance=this;Settings = GetSettings<AuditorSettings>();
            BillProgressHooks.Install();TaskPriorityHooks.Install();
        }
        public override string SettingsCategory() { return "Colony Task Auditor"; }
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(new Rect(rect.x, rect.y, rect.width, 315f));
            list.Label("CTA_Description".Translate());
            if(!BillProgressHooks.Available)list.Label("CTA_ProgressHookUnavailable".Translate());
            list.Label("CTA_Threshold".Translate(Settings.WarningDays));
            Settings.WarningDays = (int)list.Slider(Settings.WarningDays, 1, 10);
            list.CheckboxLabeled("CTA_Designations".Translate(), ref Settings.Designations);
            list.CheckboxLabeled("CTA_Bills".Translate(), ref Settings.Bills);
            list.CheckboxLabeled("CTA_Growing".Translate(), ref Settings.Farming);
            list.CheckboxLabeled("CTA_Unfinished".Translate(), ref Settings.Unfinished);
            list.CheckboxLabeled("CTA_Gathering".Translate(), ref Settings.Gathering);
            list.CheckboxLabeled("CTA_Notifications".Translate(), ref Settings.Notifications);
            list.CheckboxLabeled("CTA_ShowAlert".Translate(), ref Settings.ShowAlert);
            list.End();

            if(Widgets.ButtonText(new Rect(rect.x+230,rect.y+318,220,28),"CTA_AutomationSettings".Translate()))
                Find.WindowStack.Add(new Window_TaskAutomationSettings());
            Map current = Find.CurrentMap;
            if (current == null) return;
            TaskAuditComponent audit = current.GetComponent<TaskAuditComponent>();
            if (audit == null) return;
            if(Widgets.ButtonText(new Rect(rect.x,rect.y+318,220,28),"CTA_OpenList".Translate()))
            {
                Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
                if(!Find.WindowStack.IsOpen<Window_TaskAudit>())Find.WindowStack.Add(new Window_TaskAudit());
                return;
            }
            view.Draw(new Rect(rect.x,rect.y+352,rect.width,Math.Max(110f,rect.height-352)),audit,current,true);
        }
    }
}
