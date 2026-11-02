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
    }
}
