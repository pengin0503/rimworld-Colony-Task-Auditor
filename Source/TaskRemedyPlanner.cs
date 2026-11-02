namespace Pengin.ColonyTaskAuditor
{
    public enum ActionMode { Off, Confirm, Auto }
    public sealed class TaskRemedy
    {
        public string Hint;
        public bool CanExecute,CanAutomate;
    }
    public static class TaskRemedyPlanner
    {
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
