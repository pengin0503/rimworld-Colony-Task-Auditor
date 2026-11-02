using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class WorkAssistancePreview
    {
        public string TaskId,Blocked="CTA_AssistanceBlocked";
        public Pawn Pawn;
        public WorkGiverDef Giver;
        public WorkTypeDef Work;
        public IntVec3 Cell;
        public int Priority,Tick;
        public bool Ready {get{return Blocked==null;}}
    }
    public static class TemporaryWorkAssistance
    {
        static readonly FieldInfo priorityTick=typeof(PriorityWork).GetField("prioritizeTick",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly string[] supported={"WorkGiver_Miner","WorkGiver_ConstructFinishFrames","WorkGiver_GrowerSow","WorkGiver_GrowerHarvest","WorkGiver_DoBill",
            "WorkGiver_ConstructDeliverResourcesToBlueprints","WorkGiver_ConstructDeliverResourcesToFrames"};
        public static int RequestTick(PriorityWork work){return priorityTick==null?-1:(int)priorityTick.GetValue(work);}
        static bool? priorityConflict;
        public static bool PriorityConflict()
        {
            if(priorityConflict.HasValue)return priorityConflict.Value;
            priorityConflict=LoadedModManager.RunningModsListForReading.Any(m=>new[]{"worktab","workroles","smartpriorit","workmanager"}.Any(n=>m.PackageId.ToLowerInvariant().Contains(n)));
            return priorityConflict.Value;
        }
        public static bool IdleSafe(Pawn p,Map map)
        {
            if(p==null||!p.Spawned||p.Map!=map||!p.IsColonistPlayerControlled||p.IsColonyMechPlayerControlled||p.Drafted||p.Downed||p.InMentalState||!p.Awake()||p.jobs==null||p.mindState==null||p.mindState.duty!=null)return false;
            if(p.health.HasHediffsNeedingTend()||p.jobs.jobQueue.Count>0||p.CurJob==null||p.CurJob.playerForced)return false;
            string job=p.CurJob.def.defName;
            // Request only at idle. Never interrupt rescue, medicine, fire, sleep or any work.
            if(job!="Wait_Wander"&&job!="Wait"&&job!="GotoWander")return false;
            return p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)&&p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }
        public static WorkAssistancePreview Preview(TaskAuditComponent audit,string taskId,Pawn pawn)
        {
            var preview=new WorkAssistancePreview{TaskId=taskId,Pawn=pawn,Tick=Find.TickManager.TicksGame};
            var detail=TaskDetails.Inspect(audit,taskId,false,0);var r=detail.Record;
            if(!detail.Current||audit.PartialScan||r==null||r.Held||!r.Clock.IsStalled(audit.Threshold(r))||detail.Work==null||priorityTick==null)return preview;
            // Only a factually schedulable task can be enriched to Ready; known blockers,
            // queues and pauses are never reclassified merely because another job exists.
            if(r.Reason!="CTA_Unknown"&&r.Reason!="CTA_Ready")return preview;
            var map=audit.map;var ctx=detail.Context;
            if(audit.ProofBudget--<=0)return preview;
            if(!IdleSafe(pawn,map)||pawn.workSettings==null||pawn.WorkTypeIsDisabled(detail.Work)||pawn.workSettings.GetPriority(detail.Work)==0||pawn.mindState.priorityWork.IsPrioritized)return preview;
            if(map.mapPawns.AllPawnsSpawned.Any(p=>!p.Downed&&p.HostileTo(Faction.OfPlayer))||map.listerThings.ThingsOfDef(ThingDefOf.Fire).Count>0)return preview;
            if(!ctx.Cell.InBounds(map)||ctx.Cell.GetDangerFor(pawn,map)!=Danger.None||!ctx.Cell.InAllowedArea(pawn))return preview;
            if(ctx.Target!=null&&(ctx.Target.IsForbidden(pawn)||!pawn.CanReserveAndReach(ctx.Target,PathEndMode.Touch,Danger.None)))return preview;
            if(ctx.Bill!=null&&(ctx.Bill.suspended||!ctx.Bill.ShouldDoNow()||!ctx.Bill.PawnAllowedToStartAnew(pawn)||
                ctx.Bill.billStack.FirstShouldDoNow!=ctx.Bill||ctx.Bill.billStack.Bills.Count(b=>b.ShouldDoNow())!=1))return preview;
            foreach(var giverDef in DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g=>g.workType==detail.Work&&g.giverClass!=null&&supported.Contains(g.giverClass.Name)).Take(16))
            {
                if(giverDef.giverClass.Assembly!=typeof(Pawn).Assembly)continue;
                var giver=giverDef.Worker as WorkGiver_Scanner;if(giver==null||giver.GetType()!=giverDef.giverClass||giver.ShouldSkip(pawn,false)||giver.MissingRequiredCapacity(pawn)!=null)continue;
                if(audit.ProofBudget--<=0)return preview;
                bool job=ctx.Target!=null?giver.HasJobOnThing(pawn,ctx.Target,false):giver.HasJobOnCell(pawn,ctx.Cell,false);
                if(!job)continue;
                // Confirm the giver performs this task category, not an unrelated operation
                // on the same Thing. Vanilla bill givers are checked separately above.
                bool matches=ctx.Bill!=null?giverDef.giverClass.Name=="WorkGiver_DoBill":
                    taskId.StartsWith("construction:")?giverDef.giverClass.Name.StartsWith("WorkGiver_Construct"):
                    taskId.StartsWith("grow:")?giverDef.giverClass.Name=="WorkGiver_GrowerSow":
                    taskId.StartsWith("harvest:")?giverDef.giverClass.Name=="WorkGiver_GrowerHarvest":
                    taskId.StartsWith("designation:Mine:")&&giverDef.giverClass.Name=="WorkGiver_Miner";
                if(!matches)continue;
                preview.Giver=giverDef;preview.Cell=ctx.Cell;preview.Work=detail.Work;preview.Priority=pawn.workSettings.GetPriority(detail.Work);preview.Blocked=null;return preview;
            }
            return preview;
        }
        public static void PublishReadyFacts(TaskAuditComponent audit)
        {
            // Only inspect selected important tasks, within a small extra proof budget.
            // Unknown remains advisory unless an exact supported scanner proves the work
            // executable now by a safe idle enabled colonist. No causal guess is published.
            foreach(var watch in audit.map.GetComponent<TaskActionState>().Watches.Take(4))
            {
                var record=audit.FindRecord(watch.Info.Id);
                if(record==null||record.Reason!="CTA_Unknown"||record.Held)continue;
                var details=TaskDetails.Inspect(audit,record.Id,false,8);
                foreach(var candidate in details.Workers.Where(w=>w.Status=="CTA_WorkerCandidate").Take(8))
                    if(Preview(audit,record.Id,candidate.Pawn).Ready)
                    {record.Reason="CTA_Ready";record.Confidence="CTA_Confirmed";break;}
            }
        }
        public static bool Execute(TaskAuditComponent audit,WorkAssistancePreview shown,bool automatic)
        {
            var settings=TaskAuditorMod.Settings;var state=audit.map.GetComponent<TaskActionState>();
            if(automatic){if(!TemporaryWorkPolicy.FreshScan(Find.TickManager.TicksGame,audit.LastScanTick))return false;}
            else audit.Scan();
            var live=Preview(audit,shown.TaskId,shown.Pawn);
            if(!live.Ready||shown.Giver!=live.Giver||shown.Cell!=live.Cell||shown.Priority!=live.Priority)return false;
            var watch=state.FindWatch(shown.TaskId);int now=Find.TickManager.TicksGame;
            var facts=new AssistanceFacts{Complete=!audit.PartialScan,Current=true,VerifiedJob=true,Idle=IdleSafe(live.Pawn,audit.map),Safe=true,Enabled=true,Vanilla=true,
                PriorityConflict=PriorityConflict(),HasManualPriority=live.Pawn.mindState.priorityWork.IsPrioritized};
            // Auto additionally requires a persisted confirmed Ready diagnosis. A preview
            // alone can offer Confirm, but cannot promote an unknown cause to Auto.
            var record=audit.Records.FirstOrDefault(r=>r.Id==shown.TaskId);
            bool known=record!=null&&record.Reason=="CTA_Ready"&&record.Confidence=="CTA_Confirmed";
            if(automatic&&!known)return false;
            if(!TemporaryWorkPolicy.CanStart(facts,settings.AssistanceMode,automatic,watch!=null,watch!=null&&watch.AutoAssist,now,state.LastAction,settings.Cooldown,state.Active.Count,settings.MaxAssistance))return false;
            if(state.Active.Any(x=>x.Pawn==live.Pawn||x.TaskId==shown.TaskId))return false;
            live.Pawn.mindState.priorityWork.Set(live.Cell,live.Giver);
            int tick=RequestTick(live.Pawn.mindState.priorityWork);
            if(tick<0){live.Pawn.mindState.priorityWork.Clear();return false;}
            state.Active.Add(new ActiveTaskAssistance{TaskId=shown.TaskId,Pawn=live.Pawn,Giver=live.Giver,Cell=live.Cell,RequestTick=tick,Expires=now+settings.AssistanceDuration,Automatic=automatic,Work=live.Work,ExpectedPriority=live.Priority});
            state.LastAction=now;state.AddHistory(shown.TaskId+" / "+live.Pawn.ThingID+" / "+live.Giver.defName);
            return true;
        }
    }
    public sealed class ActiveTaskAssistance : IExposable
    {
        public string TaskId;public Pawn Pawn;public WorkGiverDef Giver;public IntVec3 Cell;
        public int RequestTick,Expires,ExpectedPriority;public WorkTypeDef Work;public bool Automatic;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref Work,"work");Scribe_Values.Look(ref ExpectedPriority,"expectedPriority",0);
            Scribe_Values.Look(ref TaskId,"taskId","");Scribe_References.Look(ref Pawn,"pawn");Scribe_Defs.Look(ref Giver,"giver");
            Scribe_Values.Look(ref Cell,"cell",IntVec3.Invalid);Scribe_Values.Look(ref RequestTick,"requestTick",-1);Scribe_Values.Look(ref Expires,"expires",0);Scribe_Values.Look(ref Automatic,"automatic",false);
        }
    }
}
