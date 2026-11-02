using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class TaskWatchEntry : IExposable
    {
        public WatchInfo Info=new WatchInfo();
        public void ExposeData()
        {
            Scribe_Values.Look(ref Info.Id,"id","");Scribe_Values.Look(ref Info.MapId,"mapId",-1);
            Scribe_Values.Look(ref Info.Threshold,"threshold",30000);Scribe_Values.Look(ref Info.AutoAssist,"autoAssist",false);
        }
    }
    public sealed class TaskActionState : MapComponent
    {
        public List<TaskWatchEntry> Watches=new List<TaskWatchEntry>();
        public int Revision;
        // Persist the next watch for fair bounded readiness checks.
        public int WatchCursor;
        int batchScan=-1;
        int batchRevision=-1;
        readonly List<TaskWatchEntry> watchBatch=new List<TaskWatchEntry>();
        public IReadOnlyList<TaskWatchEntry> GetWatchBatch(int scanTick)
        {
            if(scanTick!=batchScan||Revision!=batchRevision)
            {
                watchBatch.Clear();
                int[] indices=TaskWatchlist.BatchIndices(Watches.Count,WatchCursor,4);
                foreach(int index in indices)
                    if(Watches[index]!=null&&Watches[index].Info!=null)watchBatch.Add(Watches[index]);
                if(Watches.Count>0)WatchCursor=(WatchCursor+indices.Length)%Watches.Count;
                else WatchCursor=0;
                batchScan=scanTick;batchRevision=Revision;
            }
            return watchBatch;
        }
        int indexedRevision=-1;
        readonly Dictionary<string,WatchInfo> watchIndex=new Dictionary<string,WatchInfo>();
        public List<ActiveTaskAssistance> Active=new List<ActiveTaskAssistance>();
        public List<string> History=new List<string>();
        public int LastAction=-600000;
        int autoScan=-1;bool faulted;
        public void ExternalPriorityWrite(Pawn pawn){ExternalPriorityWrite(pawn,PriorityWriteSource.External);}
        // The request is always released and never rewritten. Auto permission is withdrawn
        // only when the write is not a positively identified vanilla end (#28).
        public void ExternalPriorityWrite(Pawn pawn,PriorityWriteSource source)
        {
            if(source==PriorityWriteSource.Owned)return;
            for(int i=Active.Count-1;i>=0;i--)
            {
                var entry=Active[i];if(entry.Pawn!=pawn)continue;
                var watch=FindWatch(entry.TaskId);
                if(watch!=null&&!TemporaryWorkPolicy.KeepsAutoPermission(source))watch.AutoAssist=false;
                Active.RemoveAt(i);Revision++;AddHistory(entry.TaskId+" "+TemporaryWorkPolicy.HistoryKey(source));
            }
        }
        public void AddHistory(string text){History.Add(Find.TickManager.TicksGame+": "+text);while(History.Count>20)History.RemoveAt(0);}
        public override void MapComponentTick()
        {
            if(faulted)return;
            try{TickActions();}catch(Exception e){faulted=true;Log.Warning("Colony Task Auditor assistance stopped: "+e.GetType().Name);}
        }
        void TickActions()
        {
            var settings=TaskAuditorMod.Settings;if(settings==null)return;
            int now=Find.TickManager.TicksGame;var audit=map.GetComponent<TaskAuditComponent>();
            if(now%250!=map.uniqueID%250&&audit.LastScanTick==autoScan&&settings.AssistanceMode!=ActionMode.Off)return;
            foreach(var entry in Active.ToList())
            {
                var pawn=entry.Pawn;var priority=pawn==null||pawn.mindState==null?null:pawn.mindState.priorityWork;
                bool owns=pawn!=null&&pawn.Spawned&&pawn.Map==map&&priority!=null&&priority.IsPrioritized&&entry.Giver!=null&&TemporaryWorkPolicy.ClearOwned(entry.RequestTick,entry.Giver.defName,new[]{entry.Cell.x,entry.Cell.z},
                    TemporaryWorkAssistance.RequestTick(priority),priority.WorkGiver==null?null:priority.WorkGiver.defName,new[]{priority.Cell.x,priority.Cell.z});
                var watch=FindWatch(entry.TaskId);var record=audit.FindRecord(entry.TaskId);
                bool absent=!audit.PartialScan&&audit.LastScanTick>0&&(record==null||record.LastSeen!=audit.LastScanTick);
                bool priorityChanged=pawn!=null&&pawn.workSettings!=null&&entry.Work!=null&&pawn.workSettings.GetPriority(entry.Work)!=entry.ExpectedPriority;
                if(priorityChanged&&watch!=null){watch.AutoAssist=false;Revision++;}
                bool stop=priorityChanged||(pawn!=null&&pawn.CurJob!=null&&pawn.CurJob.playerForced)||settings.AssistanceMode==ActionMode.Off||now>=entry.Expires||pawn==null||!pawn.Spawned||pawn.Map!=map||pawn.Drafted||pawn.Downed||pawn.InMentalState||absent||
                    (entry.Automatic&&(settings.AssistanceMode!=ActionMode.Auto||watch==null||!watch.AutoAssist||TemporaryWorkAssistance.PriorityConflict()));
                if(owns&&!stop)continue;
                if(owns)TaskPriorityHooks.Clear(priority); // CAS: changed/manual priority requests are left untouched.
                Active.Remove(entry);AddHistory("released "+entry.TaskId);
            }
            bool newScan=autoScan!=audit.LastScanTick;autoScan=audit.LastScanTick;
            if(settings.AssistanceMode!=ActionMode.Auto||!newScan||audit.PartialScan||!TemporaryWorkPolicy.FreshScan(now,audit.LastScanTick))return;
            if(TemporaryWorkAssistance.PriorityConflict()||(long)now-LastAction<settings.Cooldown)return;
            foreach(var watch in GetWatchBatch(audit.LastScanTick).Where(w=>w.Info.AutoAssist))
            {
                var record=audit.FindRecord(watch.Info.Id);
                if(record==null||record.Reason!="CTA_Ready"||record.Confidence!="CTA_Confirmed")continue;
                var detail=TaskDetails.Inspect(audit,record.Id,false,8);
                foreach(var candidate in detail.Workers.Where(w=>w.Status=="CTA_WorkerCandidate").Take(8))
                {
                    var preview=TemporaryWorkAssistance.Preview(audit,record.Id,candidate.Pawn);
                    if(preview.Ready&&TemporaryWorkAssistance.Execute(audit,preview,true))return;
                }
            }
        }
        public TaskActionState(Map map):base(map){}
        public WatchInfo FindWatch(string id)
        {
            if(indexedRevision!=Revision)
            {
                watchIndex.Clear();
                foreach(var entry in Watches)if(entry!=null&&entry.Info!=null&&entry.Info.MapId==map.uniqueID&&!string.IsNullOrEmpty(entry.Info.Id))watchIndex[entry.Info.Id]=entry.Info;
                indexedRevision=Revision;
            }
            WatchInfo result;return id!=null&&watchIndex.TryGetValue(id,out result)?result:null;
        }
        public void Toggle(string id)
        {
            var existing=Watches.FirstOrDefault(w=>TaskWatchlist.Matches(w.Info,id,map.uniqueID));
            if(existing!=null)Watches.Remove(existing);
            else if(Watches.Count<100)Watches.Add(new TaskWatchEntry{Info=new WatchInfo{Id=id,MapId=map.uniqueID,Threshold=TaskAuditorMod.Settings.WatchThreshold}});
            Revision++;
        }
        public void Prune(IEnumerable<string> present,bool complete)
        {
            var ids=new HashSet<string>(present);
            if(Watches.RemoveAll(w=>!TaskWatchlist.Keep(w.Info,ids.Contains(w.Info.Id),complete,map.uniqueID))>0)Revision++;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Active,"activeTaskAssistance",LookMode.Deep);Scribe_Collections.Look(ref History,"taskActionHistory",LookMode.Value);
            Scribe_Values.Look(ref LastAction,"lastTaskAction",-600000);
            Scribe_Values.Look(ref WatchCursor,"watchCursor",0);
            if(Active==null)Active=new List<ActiveTaskAssistance>();if(History==null)History=new List<string>();
            Scribe_Collections.Look(ref Watches,"taskWatchlist",LookMode.Deep);
            if(Watches==null)Watches=new List<TaskWatchEntry>();Revision++;
        }
    }
}
