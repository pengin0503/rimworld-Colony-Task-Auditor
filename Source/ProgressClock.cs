using System;

namespace Pengin.ColonyTaskAuditor
{
    // Primitive state only: serialized by AuditRecord, tested without loading the game.
    public sealed class ProgressClock
    {
        public bool Initialized, WasEligible, Notified;
        public int FirstSeen, LastSeen, LastProgress, Started = -1, EligibleTicks;
        public float Progress;

        public void Observe(int now, float progress, bool working, bool eligible, int maxGap)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) eligible = false;
            if (!Initialized || now < LastSeen)
            {
                Initialized = true;
                FirstSeen = LastProgress = LastSeen = now;
                Started = -1;
                EligibleTicks = 0;
                Progress = progress;
                Notified = false;
                WasEligible = false;
            }
            int elapsed = now - LastSeen;
            bool changed = progress != Progress;
            // Changes in either direction restart observation: a reset/reconfigured bill
            // or crop cycle is not evidence of an unchanged task.
            if (changed || working || elapsed > maxGap || !eligible)
            {
                EligibleTicks = 0;
                Notified = false;
            }
            else if (WasEligible && eligible)
                EligibleTicks = (int)Math.Min(int.MaxValue, (long)EligibleTicks + elapsed);
            if (changed) LastProgress = now;
            if (working && Started < 0) Started = now;
            Progress = progress;
            LastSeen = now;
            WasEligible = eligible && !working;
        }

        public void MarkProgress(int now)
        {
            if(now<0)return;
            LastProgress=now;EligibleTicks=0;WasEligible=false;Notified=false;
        }
        public bool IsStalled(int threshold)
        {
            return Initialized && WasEligible && EligibleTicks >= Math.Max(1, threshold);
        }
    }
}
