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
        {return w==null?ordinary:Math.Min(ordinary,Math.Max(250,Math.Min(600000,w.Threshold)));}
        // Bounded, fair window over a persistent watch cursor. Never scale proof
        // budgets with watch count; wrap only the index, not the work itself.
        public static int[] BatchIndices(int count,int cursor,int limit)
        {
            if(count<=0||limit<=0)return new int[0];
            int size=Math.Min(count,limit);
            int start=((cursor%count)+count)%count;
            var result=new int[size];
            for(int i=0;i<size;i++)result[i]=(start+i)%count;
            return result;
        }
        public static bool Keep(WatchInfo w,bool present,bool complete,int map)
        {return w!=null&&w.MapId==map&&(present||!complete);}
    }
}
