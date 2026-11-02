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
        public List<Thing> MaterialTargets=new List<Thing>();
        public List<Building_WorkTable> RecipeTargets=new List<Building_WorkTable>();
        public bool Current;
        internal TaskContext Context;
        public static TaskDetails Inspect(TaskAuditComponent audit,string id,bool includeMaterials=true,int workerLimit=64)
        {
            var result=new TaskDetails();TaskContext ctx;
            var r=audit.FindRecord(id);result.Record=r;
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
            foreach(var pawn in map.mapPawns.AllPawnsSpawned.Where(p=>p.IsColonistPlayerControlled||p.IsColonyMechPlayerControlled).Take(workerLimit))
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
            if(!includeMaterials)return result;
            if(ctx.Bill!=null&&ctx.Bill.recipe.skillRequirements!=null)
                foreach(var requirement in ctx.Bill.recipe.skillRequirements)result.Materials.Add("CTA_SkillRequirement".Translate(requirement.skill.LabelCap,requirement.minLevel));
            if(ctx.Target is Frame||ctx.Target is Blueprint_Build)
            {
                var def=ctx.Target.def.entityDefToBuild;
                if(def!=null)
                {
                    result.Materials.Add("CTA_SkillRequirement".Translate(SkillDefOf.Construction.LabelCap,def.constructionSkillPrerequisite));
                    result.Materials.Add("CTA_SkillRequirement".Translate(SkillDefOf.Artistic.LabelCap,def.artisticSkillPrerequisite));
                }
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
            int permitted=0,forbidden=0,scanned=0;
            foreach(var thing in map.listerThings.ThingsOfDef(def))
            {
                if(++scanned>12000)break;
                if(thing.IsForbidden(Faction.OfPlayer))forbidden+=thing.stackCount;
                else
                {
                    permitted+=thing.stackCount;
                    if(result.MaterialTargets.Count<8&&!result.MaterialTargets.Contains(thing))
                        result.MaterialTargets.Add(thing);
                }
            }
            result.Materials.Add("CTA_MaterialDetail".Translate(def.LabelCap,needed,permitted,forbidden));
            if(needed<=permitted||result.Recipes.Count>=8)return;
            foreach(var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Take(32))
                foreach(var recipe in bench.def.AllRecipes.Where(r=>r.products.Any(p=>p.thingDef==def)).Take(2))
                {
                    if(result.Recipes.Count>=8)return;
                    result.Recipes.Add(bench.LabelShort+" / "+recipe.LabelCap);
                    if(!result.RecipeTargets.Contains(bench))result.RecipeTargets.Add(bench);
                }
        }
    }
}
