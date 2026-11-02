using System;
using Pengin.ColonyTaskAuditor;
static class RemedyTests
{
    public static void Run()
    {
        foreach(string r in new[]{"CTA_Unknown","CTA_Paused","CTA_Forbidden","CTA_Queued","CTA_Working","CTA_Waiting","CTA_MineQueued"})
            if(TaskRemedyPlanner.Plan(r,true,false,false,false,true).CanAutomate)throw new Exception("unsafe reason: "+r);
        if(TaskRemedyPlanner.Plan("CTA_MaterialMissing",false,false,false,false,true).CanExecute)throw new Exception("inferred shortage is advisory");
        if(TaskRemedyPlanner.Plan("CTA_Ready",true,true,false,false,true).CanExecute)throw new Exception("partial scan blocks writes");
        if(TaskRemedyPlanner.Plan("CTA_Ready",true,false,true,false,true).CanExecute)throw new Exception("completed task blocks writes");
        if(TaskRemedyPlanner.Plan("CTA_Ready",true,false,false,true,true).CanExecute)throw new Exception("backlog blocks writes");
        if(!TaskRemedyPlanner.Plan("CTA_Ready",true,false,false,false,true).CanAutomate)throw new Exception("confirmed ready job can be assisted");
        if(TaskRemedyPlanner.Plan("CTA_Ready",true,false,false,false,false).CanExecute)throw new Exception("live job proof required");
        if(TaskRemedyPlanner.Plan("CTA_Skill",true,false,false,false,false).Hint==TaskRemedyPlanner.Plan("CTA_Unreachable",true,false,false,false,false).Hint)throw new Exception("cause-specific remedies");
        if(TaskRemedyPlanner.NavigationFor("CTA_MaterialMissing")!=RemedyNavigation.Materials)
            throw new Exception("material blockage should offer stock or bench navigation");
        if(TaskRemedyPlanner.NavigationFor("CTA_NoWorker")!=RemedyNavigation.Worker)
            throw new Exception("missing worker should offer candidate selection");
        if(TaskRemedyPlanner.NavigationFor("CTA_Unreachable")!=RemedyNavigation.Target)
            throw new Exception("path blocker should offer target navigation");
        if(TaskRemedyPlanner.NavigationFor("CTA_Paused")!=RemedyNavigation.Target)
            throw new Exception("manual pause should offer target inspection, not direct changes");
        Console.WriteLine("PASS 18 remedy policy scenarios");
    }
}
