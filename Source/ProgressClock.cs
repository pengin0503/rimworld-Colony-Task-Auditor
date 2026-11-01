using System;

namespace Pengin.ColonyTaskAuditor
{
    // Primitive state only: serialized by AuditRecord, tested without loading the game.
    public sealed class ProgressClock
    {
        public bool Initialized, WasEligible, Notified,HasProgress;
        public int FirstSeen, LastSeen, LastProgress, Started = -1, EligibleTicks;
        public float Progress;

        public void Observe(int now, float progress, bool working, bool eligible, int maxGap)
        {
            Observe(now, progress, working, eligible, maxGap, false);
        }

        // hold: workers are temporarily unavailable (asleep, drafted, downed). The clock is
        // frozen instead of reset, so a nightly sleep neither ages nor erases the episode.
        // The interval after a hold is not counted because the wake-up time is unknown.
        public void Observe(int now, float progress, bool working, bool eligible, int maxGap, bool hold)
        {
            Observe(now, progress, working, eligible, maxGap, hold, false);
        }

        // increasingOnly: only an increase of the metric is progress. Used for pending-work
        // counts encoded as -count (crops ripening or new empty cells raise the backlog;
        // that is new work, not completed work), so the baseline follows without a reset.
        public void Observe(int now, float progress, bool working, bool eligible, int maxGap, bool hold, bool increasingOnly)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) eligible = false;
            if (!Initialized || now < LastSeen)
            {
                Initialized = true;
                FirstSeen = LastProgress = LastSeen = now;
                Started = -1;
                EligibleTicks = 0;
                Progress = progress;
                Notified = false;HasProgress=false;
                WasEligible = false;
            }
            int elapsed = now - LastSeen;
            bool changed = increasingOnly ? progress > Progress : progress != Progress;
            if (hold && eligible && !changed && !working && elapsed <= maxGap)
            {
                Progress = progress;
                LastSeen = now;
                WasEligible = false;
                return;
            }
            // Changes in either direction restart observation: a reset/reconfigured bill
            // or crop cycle is not evidence of an unchanged task.
            if (changed || working || elapsed > maxGap || !eligible)
            {
                EligibleTicks = 0;
                Notified = false;
            }
            else if (WasEligible && eligible)
                EligibleTicks = (int)Math.Min(int.MaxValue, (long)EligibleTicks + elapsed);
            if (changed) {LastProgress = now;HasProgress=true;}
            if (working && Started < 0) Started = now;
            Progress = progress;
            LastSeen = now;
            WasEligible = eligible && !working;
        }

        public void MarkProgress(int now)
        {
            if(now<0)return;
            LastProgress=now;HasProgress=true;EligibleTicks=0;WasEligible=false;Notified=false;
        }
        public bool IsStalled(int threshold)
        {
            return Initialized && WasEligible && EligibleTicks >= Math.Max(1, threshold);
        }

        // Display-only: an episode that already reached the threshold stays visible while
        // the clock is frozen by a temporary hold. Alerts still require IsStalled.
        public bool ReachedThreshold(int threshold)
        {
            return Initialized && EligibleTicks >= Math.Max(1, threshold);
        }
    }
}
