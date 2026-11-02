using System;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class WatchInfo
    {
        public string Id;
        public int MapId,Threshold=30000;
        public bool AutoAssist;
    }
    public static class TaskWatchlist
    {
        public static bool Matches(WatchInfo w,string id,int map){return w!=null&&w.MapId==map&&w.Id==id;}
        public static int Threshold(WatchInfo w,int ordinary,ActionMode mode)
        {return mode==ActionMode.Off||w==null?ordinary:Math.Min(ordinary,Math.Max(250,Math.Min(600000,w.Threshold)));}
        public static bool Keep(WatchInfo w,bool present,bool complete,int map)
        {return w!=null&&w.MapId==map&&(present||!complete);}
    }
}
