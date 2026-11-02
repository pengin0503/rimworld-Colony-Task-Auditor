using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class WorkerDiagnosis
    {
        public Pawn Pawn;
        public string Status;
    }
    public sealed class TaskDetails
    {
        public AuditRecord Record;
        public WorkTypeDef Work;
        public List<WorkerDiagnosis> Workers=new List<WorkerDiagnosis>();
        public List<string> Materials=new List<string>(),Recipes=new List<string>();
        public bool Current;
        internal TaskContext Context;
        public static TaskDetails Inspect(TaskAuditComponent audit,string id)
        {
            var result=new TaskDetails();TaskContext ctx;
            var r=audit.Records.FirstOrDefault(x=>x.Id==id);result.Record=r;
            if(r==null||r.LastSeen!=audit.LastScanTick||!audit.Contexts.TryGetValue(id,out ctx))return result;
            result.Context=ctx;var map=audit.map;
            if(ctx.Target!=null&&(!ctx.Target.Spawned||ctx.Target.Map!=map))return result;
            if(ctx.Bill!=null&&(ctx.Bill.billStack==null||!ctx.Bill.billStack.Bills.Contains(ctx.Bill)))return result;
            result.Current=true;
            var work=ctx.Work??(ctx.Bill==null?null:ctx.Bill.recipe.requiredGiverWorkType);
            if(work==null&&ctx.Bench!=null)
            {
                var types=DefDatabase<WorkGiverDef>.AllDefsListForReading.Where(g=>g.fixedBillGiverDefs!=null&&g.fixedBillGiverDefs.Contains(ctx.Bench.def)).Select(g=>g.workType).Where(w=>w!=null).Distinct().ToList();
                if(types.Count==1)work=types[0];
            }
            result.Work=work;
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.Where(p=>p.IsColonistPlayerControlled||p.IsColonyMechPlayerControlled).Take(64))
            {
                string status="CTA_WorkerCandidate";
                if(pawn.IsColonyMechPlayerControlled)status="CTA_WorkerSpecial";
                else if(work==null||pawn.workSettings==null)status="CTA_WorkerUnsupported";
                else if(pawn.WorkTypeIsDisabled(work))status="CTA_WorkerIncapable";
                else if(pawn.workSettings.GetPriority(work)==0)status="CTA_WorkerOff";
                else if(pawn.Drafted||pawn.Downed||pawn.InMentalState||!pawn.Awake())status="CTA_WorkerUnavailable";
                else if(ctx.Bill!=null&&!ctx.Bill.PawnAllowedToStartAnew(pawn))status="CTA_WorkerRestriction";
                else if(ctx.Bill!=null&&!ctx.Bill.recipe.PawnSatisfiesSkillRequirements(pawn))status="CTA_WorkerSkill";
                else if(ctx.Skill!=null&&(pawn.skills==null||pawn.skills.GetSkill(ctx.Skill).Level<ctx.MinSkill))status="CTA_WorkerSkill";
                else if(!pawn.CanReach(ctx.Target!=null?new LocalTargetInfo(ctx.Target):new LocalTargetInfo(ctx.Cell),PathEndMode.Touch,Danger.None))status="CTA_WorkerReach";
                result.Workers.Add(new WorkerDiagnosis{Pawn=pawn,Status=status});
            }
            var construction=ctx.Target as IConstructible;
            if(construction!=null)
            {
                foreach(var cost in construction.TotalMaterialCost().Take(16))
                {
                    var frame=ctx.Target as Frame;int needed=frame==null?cost.count:frame.ThingCountNeeded(cost.thingDef);
                    AddMaterial(result,map,cost.thingDef,needed);
                }
            }
            if(ctx.Bill!=null)
            {
                int budget=64;
                foreach(var ingredient in ctx.Bill.recipe.ingredients.Take(16))
                    foreach(var def in ingredient.filter.AllowedThingDefs.Where(d=>ctx.Bill.ingredientFilter.Allows(d)).Take(8))
                    {
                        if(--budget<0)break;
                        AddMaterial(result,map,def,ingredient.CountRequiredOfFor(def,ctx.Bill.recipe));
                    }
            }
            return result;
        }
        static void AddMaterial(TaskDetails result,Map map,ThingDef def,int needed)
        {
            var stacks=map.listerThings.ThingsOfDef(def).Take(12000).ToList();
            int permitted=stacks.Where(t=>!t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
            int forbidden=stacks.Where(t=>t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
            result.Materials.Add("CTA_MaterialDetail".Translate(def.LabelCap,needed,permitted,forbidden));
            if(needed<=permitted||result.Recipes.Count>=8)return;
            foreach(var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Take(32))
                foreach(var recipe in bench.def.AllRecipes.Where(r=>r.products.Any(p=>p.thingDef==def)).Take(2))
                {
                    if(result.Recipes.Count>=8)return;
                    result.Recipes.Add(bench.LabelShort+" / "+recipe.LabelCap);
                }
        }
    }
}
