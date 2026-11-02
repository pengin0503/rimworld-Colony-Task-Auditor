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
        public TaskActionState(Map map):base(map){}
        public WatchInfo FindWatch(string id){var e=Watches.FirstOrDefault(w=>TaskWatchlist.Matches(w.Info,id,map.uniqueID));return e==null?null:e.Info;}
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
            base.ExposeData();Scribe_Collections.Look(ref Watches,"taskWatchlist",LookMode.Deep);
            if(Watches==null)Watches=new List<TaskWatchEntry>();Revision++;
        }
    }
}
