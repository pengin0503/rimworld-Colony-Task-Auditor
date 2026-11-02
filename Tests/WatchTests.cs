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
        if(TaskWatchlist.Threshold(w,120000,ActionMode.Off)!=120000)throw new Exception("off preserves ordinary monitor");
        if(TaskWatchlist.Keep(w,false,true,7))throw new Exception("complete scan prunes vanished tasks");
        if(!TaskWatchlist.Keep(w,false,false,7))throw new Exception("partial scan cannot prove removal");
        if(TaskWatchlist.Keep(w,true,true,8))throw new Exception("another map cannot inherit permissions");
        Console.WriteLine("PASS 7 watchlist scenarios");
    }
}
