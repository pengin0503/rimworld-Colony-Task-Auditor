using System.Linq;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class AssistanceFacts
    {
        public bool Complete,Current,VerifiedJob,Idle,Safe,Enabled,Vanilla,PriorityConflict,HasManualPriority;
    }
    public static class TemporaryWorkPolicy
    {
        public static bool FreshScan(int now,int scan){return now>=scan&&(long)now-scan<=250;}
        public static bool CanStart(AssistanceFacts f,ActionMode mode,bool automatic,bool watched,bool allowed,int now,int last,int cooldown,int active,int cap)
        {
            return mode!=ActionMode.Off&&(!automatic||(mode==ActionMode.Auto&&watched&&allowed&&!f.PriorityConflict))&&
                f.Complete&&f.Current&&f.VerifiedJob&&f.Idle&&f.Safe&&f.Enabled&&f.Vanilla&&!f.HasManualPriority&&
                active<cap&&now>=last&&(long)now-last>=cooldown;
        }
        public static bool ClearOwned(int tick,string giver,int[] cell,int currentTick,string currentGiver,int[] currentCell)
        {return tick==currentTick&&giver==currentGiver&&cell.SequenceEqual(currentCell);}
        // #28: who wrote a pawn's PriorityWork while our assistance was active. Vanilla 1.7.4985
        // call sites (Assembly-CSharp): Set only from Pawn_JobTracker.TryTakeOrderedJobPrioritizedWork
        // (an ordered "prioritize" job: player or another mod). Clear from PriorityWork.IsPrioritized
        // after its 30000-tick timeout, JobGiver_Work when no prioritized job remains,
        // Pawn_MindState.Reset, ThingCountTracker.InterruptEnroutePawns, and
        // ClearPrioritizedWorkAndJobQueue (draft toggle, ritual duty, portal exit, and the
        // player's "clear prioritized work" gizmo).
        public static PriorityWriteSource ClassifyWrite(bool owned,bool isSet,bool expiredBefore,PriorityWriteSource context)
        {
            if(owned)return PriorityWriteSource.Owned;
            // Every non-owned Set is a new order, even inside an engine context.
            if(isSet)return PriorityWriteSource.External;
            // Timeout needs both: cleared from inside IsPrioritized, and the order was overdue.
            if(context==PriorityWriteSource.Timeout)return expiredBefore?PriorityWriteSource.Timeout:PriorityWriteSource.External;
            return context==PriorityWriteSource.Owned?PriorityWriteSource.External:context;
        }
        // Natural or engine-caused ends release the request but keep the player's Auto
        // permission. Anything not positively identified is handled as a manual order.
        public static bool KeepsAutoPermission(PriorityWriteSource s){return s!=PriorityWriteSource.External;}
        public static string HistoryKey(PriorityWriteSource s){return "CTA_PriorityWrite"+s;}
    }
    public enum PriorityWriteSource
    {
        Owned,         // this mod's own Set/Clear
        Timeout,       // vanilla expiry of the prioritized order
        NoWork,        // JobGiver_Work found no more prioritized job (task finished or unavailable)
        Reset,         // Pawn_MindState.Reset (downed, captured, faction change, ...)
        Drafted,       // draft toggle cleared it
        TargetChanged, // the prioritized target was removed from its hauling queue
        External       // player, another mod, or an unidentified caller
    }
}
