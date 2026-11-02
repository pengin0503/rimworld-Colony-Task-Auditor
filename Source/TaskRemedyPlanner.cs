namespace Pengin.ColonyTaskAuditor
{
    public enum ActionMode { Off, Confirm, Auto }
    public enum RemedyNavigation { None, Materials, Worker, Target }
    public sealed class TaskRemedy
    {
        public string Hint;
        public bool CanExecute,CanAutomate;
    }
    public static class TaskRemedyPlanner
    {
        // Navigation is deliberately read-only: it never changes a work policy,
        // forbidden flag, priority or job. UI must verify targets are still valid.
        public static RemedyNavigation NavigationFor(string reason)
        {
            switch(reason)
            {
                case "CTA_MaterialMissing":case "CTA_BillMaterials":case "CTA_MaterialDelivery":
                    return RemedyNavigation.Materials;
                case "CTA_NoWorker":case "CTA_NoWorkersOnMap":case "CTA_Skill":
                case "CTA_BillRestriction":return RemedyNavigation.Worker;
                case "CTA_Unreachable":case "CTA_Reserved":case "CTA_Forbidden":
                case "CTA_Paused":case "CTA_SowBlocked":return RemedyNavigation.Target;
                default:return RemedyNavigation.None;
            }
        }
        // A paused bill is an intentional player decision. It may be resumed
        // only by an explicit, current confirmation; never by Auto.
        public static bool CanResumePausedBill(string reason,bool verified,bool partial,bool suspended,bool approved)
        {return approved&&verified&&!partial&&suspended&&reason=="CTA_Paused";}
        // A live scanner proof is a fact about this exact task/worker; a likely cause alone
        // never authorizes a write. Pauses, normal queues and partial scans always win.
        public static TaskRemedy Plan(string reason,bool confirmed,bool partial,bool gone,bool held,bool verifiedJob)
        {
            string hint="CTA_RemedyInspect";
            switch(reason)
            {
                case "CTA_MaterialMissing":case "CTA_BillMaterials":hint="CTA_RemedyMaterials";break;
                case "CTA_MaterialDelivery":case "CTA_SowBlocked":hint="CTA_RemedyDelivery";break;
                case "CTA_NoWorker":case "CTA_NoWorkersOnMap":hint="CTA_RemedyAssignment";break;
                case "CTA_Skill":hint="CTA_RemedySkill";break;
                case "CTA_BillRestriction":hint="CTA_RemedyRestriction";break;
                case "CTA_Unreachable":hint="CTA_RemedyReach";break;
                case "CTA_Reserved":hint="CTA_RemedyReserved";break;
                case "CTA_Paused":case "CTA_Forbidden":hint="CTA_RemedyManualPause";break;
                case "CTA_Waiting":case "CTA_Queued":case "CTA_MineQueued":case "CTA_Working":hint="CTA_RemedyWait";break;
                case "CTA_Ready":hint="CTA_RemedyReady";break;
            }
            bool permitted=reason=="CTA_Ready"&&confirmed&&!partial&&!gone&&!held&&verifiedJob;
            return new TaskRemedy{Hint=hint,CanExecute=permitted,CanAutomate=permitted};
        }
    }
}
