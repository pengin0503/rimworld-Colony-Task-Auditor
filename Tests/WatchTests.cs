using System;
using Pengin.ColonyTaskAuditor;
static class WatchTests
{
    public static void Run()
    {
        var w=new WatchInfo{Id="bill:1",MapId=7,Threshold=6000};
        if(!TaskWatchlist.Matches(w,"bill:1",7))throw new Exception("stable bill identity");
        if(TaskWatchlist.Matches(w,"bill:1",8)||TaskWatchlist.Matches(w,"bill:2",7))throw new Exception("map/task identity");
        if(TaskWatchlist.Threshold(w,120000,ActionMode.Confirm)!=6000)throw new Exception("individual threshold");
        if(TaskWatchlist.Threshold(w,120000,ActionMode.Off)!=6000)throw new Exception("assistance off preserves important watch");
        if(TaskWatchlist.Keep(w,false,true,7))throw new Exception("complete scan prunes vanished tasks");
        if(!TaskWatchlist.Keep(w,false,false,7))throw new Exception("partial scan cannot prove removal");
        if(TaskWatchlist.Keep(w,true,true,8))throw new Exception("another map cannot inherit permissions");
        // A bounded batch must circulate past the first four watch registrations.
        int cursor=0;bool[] observed=new bool[11];
        for(int round=0;round<3;round++)
        {
            var batch=TaskWatchlist.BatchIndices(observed.Length,cursor,4);
            if(batch.Length!=4)throw new Exception("watch proof batch budget");
            foreach(int i in batch)observed[i]=true;
            cursor=(cursor+batch.Length)%observed.Length;
        }
        if(Array.Exists(observed,v=>!v))throw new Exception("watched work starvation");
        if(TaskWatchlist.BatchIndices(0,8,4).Length!=0)throw new Exception("empty watchlist rotation");
        Console.WriteLine("PASS 10 watchlist scenarios");
    }
}
