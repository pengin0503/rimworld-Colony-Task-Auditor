using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using System.Diagnostics;

namespace Pengin.ColonyTaskAuditor
{
    public sealed class AuditorSettings : ModSettings
    {
        public int WarningDays = 2;
        public ActionMode RemedyMode=ActionMode.Confirm,AssistanceMode=ActionMode.Confirm;
        public int WatchThreshold=30000,Cooldown=60000,AssistanceDuration=2500,MaxAssistance=2;
        public bool Designations = true, Bills = true, Farming = true, Unfinished = true, Gathering = true;
        public bool Notifications = true, ShowAlert = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref WarningDays, "warningDays", 2);
            Scribe_Values.Look(ref Designations, "designations", true);
            Scribe_Values.Look(ref Bills, "bills", true);
            Scribe_Values.Look(ref Farming, "farming", true);
            Scribe_Values.Look(ref Unfinished, "unfinished", true);
            Scribe_Values.Look(ref Gathering, "gathering", true);
            Scribe_Values.Look(ref Notifications, "notifications", true);
            Scribe_Values.Look(ref ShowAlert, "showAlert", true);
            Scribe_Values.Look(ref RemedyMode,"remedyMode",ActionMode.Confirm);
            Scribe_Values.Look(ref AssistanceMode,"assistanceMode",ActionMode.Confirm);
            Scribe_Values.Look(ref WatchThreshold,"watchThreshold",30000);
            Scribe_Values.Look(ref Cooldown,"assistanceCooldown",60000);
            Scribe_Values.Look(ref AssistanceDuration,"assistanceDuration",2500);
            Scribe_Values.Look(ref MaxAssistance,"maxAssistance",2);
            WatchThreshold=Math.Max(250,Math.Min(600000,WatchThreshold));Cooldown=Math.Max(2500,Cooldown);
            AssistanceDuration=Math.Max(250,Math.Min(60000,AssistanceDuration));MaxAssistance=Math.Max(1,Math.Min(8,MaxAssistance));
            base.ExposeData();
        }
    }

    public sealed class AuditRecord : IExposable
    {
        public string Id, Category, Description;
        public int FirstSeen, LastSeen, X = -1, Z = -1;
        public ProgressClock Clock = new ProgressClock();
        public string Reason = "CTA_Unknown", Confidence = "CTA_Uncertain";
        // Transient, recomputed every scan: the timer is frozen because the same work type
        // or workbench is busy elsewhere (normal backlog). Not saved.
        public bool Held;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", "");
            Scribe_Values.Look(ref Category, "category", "");
            Scribe_Values.Look(ref Description, "description", "");
            Scribe_Values.Look(ref FirstSeen, "firstSeen", 0);
            Scribe_Values.Look(ref LastSeen, "lastSeen", 0);
            Scribe_Values.Look(ref X, "x", -1);
            Scribe_Values.Look(ref Z, "z", -1);
            if (Clock == null) Clock = new ProgressClock();
            Scribe_Values.Look(ref Clock.Initialized, "progressInitialized", false);
            Scribe_Values.Look(ref Clock.FirstSeen, "progressFirstSeen", 0);
            Scribe_Values.Look(ref Clock.LastSeen, "progressLastSeen", 0);
            Scribe_Values.Look(ref Clock.LastProgress, "lastProgress", 0);
            Scribe_Values.Look(ref Clock.HasProgress,"hasProgress",false);
            Scribe_Values.Look(ref Clock.Started, "started", -1);
            Scribe_Values.Look(ref Clock.EligibleTicks, "eligibleTicks", 0);
            Scribe_Values.Look(ref Clock.Progress, "progress", 0f);
            Scribe_Values.Look(ref Clock.WasEligible, "wasEligible", false);
            Scribe_Values.Look(ref Clock.Notified, "progressNotified", false);
            Scribe_Values.Look(ref Reason, "reason", "CTA_Unknown");
            Scribe_Values.Look(ref Confidence, "confidence", "CTA_Uncertain");
        }
    }

    // Everything Diagnose needs about one monitored task. Built per scan, never saved.
    internal sealed class TaskContext
    {
        public Thing Target;
        public IntVec3 Cell = IntVec3.Invalid;
        public WorkTypeDef Work;
        public Bill Bill;
        public Building Bench;
        public Zone_Growing Zone;
        public string PausedReason;
        public SkillDef Skill;
        public int MinSkill;
        public bool SowBlocked, MineQueued;
        public Predicate<Job> Matches;
    }

    enum TaskState { Eligible, Paused, Hold, Working }

    public sealed class TaskAuditComponent : MapComponent
    {
        private const int Interval = 5000;
        // Per-scan work budgets. Categories over budget rotate through their entries and
        // keep unseen records (and their clocks) instead of pruning or resetting them.
        private const int MaxDesignations = 5000, MaxObjects = 12000, MaxBills = 1500,
            MaxZoneCellsTotal = 40000, MaxAnimals = 1000;
        private List<AuditRecord> records = new List<AuditRecord>();
        public IReadOnlyList<AuditRecord> Records { get { return records; } }
        internal int ProofBudget;
        internal AuditRecord FindRecord(string id){AuditRecord r;return recordLookup.TryGetValue(id,out r)?r:null;}
        public int Threshold(AuditRecord record)
        {return TaskWatchlist.Threshold(map.GetComponent<TaskActionState>().FindWatch(record.Id),Math.Max(1,TaskAuditorMod.Settings.WarningDays)*60000,TaskAuditorMod.Settings.AssistanceMode);}
        public int LastScanTick { get; private set; }
        public bool PartialScan { get; private set; }
        public int FailedItems { get; private set; }
        public double ScanMilliseconds { get; private set; }
        // Overdue records seen in the last scan, used by the alert (cheap to read per frame).
        public List<AuditRecord> Overdue { get; private set; } = new List<AuditRecord>();
        internal Dictionary<string,TaskContext> Contexts=new Dictionary<string,TaskContext>();
        private int designationOffset, objectOffset, constructionOffset, billOffset, zoneOffset, animalOffset;
        private List<Pawn> workers;
        private List<Job> activeJobs;
        private Dictionary<Thing,List<Job>> jobsByThing;
        private Dictionary<IntVec3,List<Job>> jobsByCell;
        private HashSet<Bill> activeBills;
        private HashSet<Thing> busyBenches;
        // Work types observed in progress since the previous scan (250-tick samples + scan).
        private HashSet<WorkTypeDef> recentWorkTypes = new HashSet<WorkTypeDef>();
        private int pathBudget, ingredientBudget;
        private Dictionary<string,AuditRecord> recordLookup = new Dictionary<string,AuditRecord>();
        private bool needsInitialScan = true;
        private Dictionary<Thing,List<AuditRecord>> targetRecords = new Dictionary<Thing,List<AuditRecord>>();
        private Dictionary<IntVec3,List<AuditRecord>> cellRecords = new Dictionary<IntVec3,List<AuditRecord>>();
        private Dictionary<string,Predicate<Job>> matchers = new Dictionary<string,Predicate<Job>>();
        private Dictionary<string,float> liveProgress = new Dictionary<string,float>();
        private HashSet<string> warned = new HashSet<string>();
        private bool activeWorkWarned;
        private Dictionary<ThingDef,int> materialStocks;
        // Dictionary keys cannot be null; "no specific work type" is a valid key here.
        sealed class WorkKey
        {
            readonly WorkTypeDef work;
            public WorkKey(WorkTypeDef work){this.work=work;}
            public override bool Equals(object o){var k=o as WorkKey;return k!=null&&k.work==work;}
            public override int GetHashCode(){return work==null?0:work.GetHashCode();}
        }
        private readonly Dictionary<WorkKey,List<Pawn>> enabledCache=new Dictionary<WorkKey,List<Pawn>>();
        private readonly Dictionary<WorkKey,List<Pawn>> availableCache=new Dictionary<WorkKey,List<Pawn>>();
        private static Dictionary<ThingDef,HashSet<WorkTypeDef>> workMappings;
        private static List<ThingDef> unfinishedDefs;
        private static readonly Dictionary<DesignationDef,Predicate<Job>> designationMatchers = new Dictionary<DesignationDef,Predicate<Job>>();

        public TaskAuditComponent(Map map) : base(map) { }

        public void NotifyBillProgress(Bill bill)
        {
            AuditRecord record;
            if(recordLookup.TryGetValue("bill:"+bill.GetUniqueLoadID(),out record))record.Clock.MarkProgress(Find.TickManager.TicksGame);
        }

        static bool IsWorker(Pawn p)
        {
            return p.IsColonistPlayerControlled || (p.IsColonyMechPlayerControlled && p.workSettings != null);
        }

        private void ObserveActiveWork()
        {
            try{ObserveActiveWorkCore();}
            catch(Exception e)
            {
                if(!activeWorkWarned){activeWorkWarned=true;Log.Warning("Colony Task Auditor active-work sampling skipped: "+e.GetType().Name);}
            }
        }
        private void ObserveActiveWorkCore()
        {
            int now=Find.TickManager.TicksGame;
            foreach(Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if(pawn.CurJob==null||!IsWorker(pawn))continue;
                Job job=pawn.CurJob;
                if(job.workGiverDef!=null&&job.workGiverDef.workType!=null)recentWorkTypes.Add(job.workGiverDef.workType);
                AuditRecord r;
                if(job.bill!=null)
                {
                    string id="bill:"+job.bill.GetUniqueLoadID();
                    if(recordLookup.TryGetValue(id,out r))
                    {
                        MarkWorking(r,now);
                        JobDriver_DoBill driver=pawn.jobs.curDriver as JobDriver_DoBill;
                        if(driver!=null)
                        {
                            float previous;
                            if(liveProgress.TryGetValue(id,out previous)&&previous!=driver.workLeft)r.Clock.MarkProgress(now);
                            liveProgress[id]=driver.workLeft;
                        }
                    }
                }
                // Zone work (sowing, harvesting) is matched by the zone of the job's cell.
                if((job.def==JobDefOf.Sow||job.def==JobDefOf.Harvest)&&job.targetA.IsValid)
                {
                    Zone_Growing zone=map.zoneManager.ZoneAt(job.targetA.Cell) as Zone_Growing;
                    if(zone!=null&&recordLookup.TryGetValue((job.def==JobDefOf.Sow?"grow:":"harvest:")+zone.ID,out r))MarkWorking(r,now);
                }
                foreach(LocalTargetInfo target in new[]{job.targetA,job.targetB,job.targetC})
                {
                    if(!target.IsValid)continue;
                    List<AuditRecord> cellMatches;
                    if(cellRecords.TryGetValue(target.Cell,out cellMatches))
                        foreach(AuditRecord match in cellMatches)
                        {
                            Predicate<Job> cellMatcher;
                            if(matchers.TryGetValue(match.Id,out cellMatcher)&&cellMatcher!=null&&cellMatcher(job))MarkWorking(match,now);
                        }
                    if(!target.HasThing)continue;
                    List<AuditRecord> matches;
                    if(!targetRecords.TryGetValue(target.Thing,out matches))continue;
                    foreach(AuditRecord match in matches)
                    {
                        // Only the job that performs this task counts; a refuel/repair/haul
                        // job on the same Thing is not progress on it.
                        Predicate<Job> matcher;
                        if(!matchers.TryGetValue(match.Id,out matcher)||matcher==null||!matcher(job))continue;
                        MarkWorking(match,now);
                        Frame frame=target.Thing as Frame;UnfinishedThing unfinished=target.Thing as UnfinishedThing;
                        float value=frame!=null?frame.workDone:unfinished!=null?-unfinished.workLeft:target.Thing.HitPoints;
                        // Live samples are compared only with earlier live samples of the same
                        // metric; the scan metric (counts, fullness, repeat count) is untouched.
                        float previous;
                        if(liveProgress.TryGetValue(match.Id,out previous)&&previous!=value)match.Clock.MarkProgress(now);
                        liveProgress[match.Id]=value;
                    }
                }
            }
        }
        static void MarkWorking(AuditRecord r,int now)
        {
            if(r.Clock.Started<0)r.Clock.Started=now;
            r.Clock.EligibleTicks=0;r.Clock.WasEligible=false;
        }

        private static void BuildStaticMappings()
        {
            if(workMappings!=null)return;
            var mappings=new Dictionary<ThingDef,HashSet<WorkTypeDef>>();
            foreach(WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if(giver.workType==null||giver.fixedBillGiverDefs==null||giver.giverClass==null||!typeof(WorkGiver_DoBill).IsAssignableFrom(giver.giverClass))continue;
                foreach(ThingDef def in giver.fixedBillGiverDefs)
                {
                    HashSet<WorkTypeDef> types;
                    if(!mappings.TryGetValue(def,out types))mappings[def]=types=new HashSet<WorkTypeDef>();
                    types.Add(giver.workType);
                }
            }
            // Unfinished products are found through their defs instead of scanning every Thing.
            unfinishedDefs=DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d=>d.thingClass!=null&&typeof(UnfinishedThing).IsAssignableFrom(d.thingClass)).ToList();
            workMappings=mappings;
        }

        static Predicate<Job> DesignationMatcher(DesignationDef def)
        {
            Predicate<Job> matcher;
            if(designationMatchers.TryGetValue(def,out matcher))return matcher;
            if(def.defName=="HarvestPlant")matcher=j=>j.def==JobDefOf.Harvest||j.def==JobDefOf.HarvestDesignated;
            else if(def.defName=="CutPlant")matcher=j=>j.def==JobDefOf.CutPlant||j.def==JobDefOf.CutPlantDesignated;
            else
            {
                // Vanilla designations share their defName with the job that performs them
                // (Mine, Deconstruct, Uninstall, Hunt, Tame, Slaughter, SmoothFloor, ...).
                // Unknown designations never count as worked: activity stays unknown.
                JobDef jobDef=DefDatabase<JobDef>.GetNamedSilentFail(def.defName);
                matcher=jobDef==null?(Predicate<Job>)(j=>false):j=>j.def==jobDef;
            }
            designationMatchers[def]=matcher;
            return matcher;
        }
        static readonly Predicate<Job> ConstructionMatcher=j=>j.def==JobDefOf.FinishFrame||j.def==JobDefOf.HaulToContainer||j.def==JobDefOf.PlaceNoCostFrame;
        static readonly Predicate<Job> UnfinishedMatcher=j=>j.def==JobDefOf.DoBill;
        static readonly Predicate<Job> SowMatcher=j=>j.def==JobDefOf.Sow;
        static readonly Predicate<Job> HarvestMatcher=j=>j.def==JobDefOf.Harvest;
        static Predicate<Job> GatherMatcher(CompHasGatherableBodyResource comp,int gatherComps)
        {
            if(comp is CompMilkable)return j=>j.def==JobDefOf.Milk;
            if(comp is CompShearable)return j=>j.def==JobDefOf.Shear;
            // A modded gathering job cannot be tied to a specific resource; accept it only
            // when the animal has a single gatherable resource.
            if(gatherComps!=1)return j=>false;
            return j=>j.def.driverClass!=null&&typeof(JobDriver_GatherAnimalBodyResources).IsAssignableFrom(j.def.driverClass);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "auditRecords", LookMode.Deep);
            if (records == null) records = new List<AuditRecord>();
        }
        public override void MapComponentTick()
        {
            if(needsInitialScan){needsInitialScan=false;Scan();}
            if(Find.TickManager.TicksGame%250==map.uniqueID%250)ObserveActiveWork();
            if (Find.TickManager.TicksGame % Interval == map.uniqueID % Interval) Scan();
        }
        public void Scan()
        {
            if (TaskAuditorMod.Settings == null ) return;
            var timer = Stopwatch.StartNew();
            try { ScanCore(); }
            catch (Exception e)
            {
                PartialScan = true;
                // Do not publish a partly mutated snapshot after a mod/API failure.
                Warn("scan", e);
            }
            finally { ScanMilliseconds = timer.Elapsed.TotalMilliseconds; }
        }
        void Warn(string where, Exception e)
        {
            if(warned.Add(where+":"+e.GetType().FullName))
                Log.Warning("Colony Task Auditor skipped "+where+": "+e.GetType().Name+" "+e.Message);
        }

        static int SkillLevel(Pawn p, SkillDef skill)
        {
            if(p.IsColonyMechPlayerControlled)
            {
                // Mechanoids work at a fixed skill level for their enabled work types.
                var types=p.RaceProps.mechEnabledWorkTypes;
                return types!=null&&types.Any(w=>w.relevantSkills!=null&&w.relevantSkills.Contains(skill))?p.RaceProps.mechFixedSkillLevel:0;
            }
            return p.skills==null?0:p.skills.GetSkill(skill).Level;
        }

        private bool IsWorked(TaskContext ctx)
        {
            if(ctx.Bench!=null)return activeBills.Contains(ctx.Bill);
            if(ctx.Matches==null)return false;
            if(ctx.Zone!=null)
                return activeJobs.Any(j=>ctx.Matches(j)&&j.targetA.IsValid&&map.zoneManager.ZoneAt(j.targetA.Cell)==ctx.Zone);
            List<Job> jobs;
            if(ctx.Target!=null&&jobsByThing.TryGetValue(ctx.Target,out jobs)&&jobs.Any(j=>ctx.Matches(j)))return true;
            return ctx.Target==null&&ctx.Cell.IsValid&&jobsByCell.TryGetValue(ctx.Cell,out jobs)&&jobs.Any(j=>ctx.Matches(j));
        }

        private TaskState Diagnose(AuditRecord r, TaskContext ctx)
        {
            r.Reason = "CTA_Unknown"; r.Confidence = "CTA_Uncertain";
            Thing target=ctx.Target; Bill bill=ctx.Bill; WorkTypeDef work=ctx.Work;
            if (ctx.PausedReason != null) { r.Reason = ctx.PausedReason; r.Confidence = "CTA_Confirmed"; return TaskState.Paused; }
            // Forbidding is a deliberate player action (e.g. deferring a blueprint): shown, not aged.
            if (target != null && target.IsForbidden(Faction.OfPlayer))
            { r.Reason = "CTA_Forbidden"; r.Confidence = "CTA_Confirmed"; return TaskState.Paused; }
            // Nobody on this map (caravan, expedition): workers exist elsewhere, so wait.
            if (workers.Count == 0) { r.Reason = "CTA_NoWorkersOnMap"; r.Confidence = "CTA_Uncertain"; return TaskState.Hold; }
            if(bill!=null&&work==null)work=bill.recipe.requiredGiverWorkType;
            HashSet<WorkTypeDef> applicable=null;
            if(bill!=null&&work==null&&target!=null)workMappings.TryGetValue(target.def,out applicable);
            // Non-bill tasks only depend on the work type: share the worker lists per scan
            // instead of rebuilding them for each of possibly thousands of designations.
            List<Pawn> enabled,cachedAvailable=null;
            WorkKey key=bill==null?new WorkKey(work):null;
            if(key!=null&&enabledCache.TryGetValue(key,out enabled))availableCache.TryGetValue(key,out cachedAvailable);
            else
            {
                enabled = workers.Where(p => p.workSettings != null &&
                    (work!=null?p.workSettings.WorkIsActive(work):applicable!=null&&applicable.Count>0?applicable.Any(p.workSettings.WorkIsActive):true)).ToList();
                if(key!=null)enabledCache[key]=enabled;
            }
            if (enabled.Count == 0 && (work != null||applicable!=null&&applicable.Count>0))
            { r.Reason = "CTA_NoWorker"; r.Confidence = "CTA_Confirmed"; return TaskState.Eligible; }
            // Install blueprints carry entityDefToBuild but need neither materials nor build skill.
            BuildableDef construction=target is Frame||target is Blueprint_Build?target.def.entityDefToBuild:null;
            if(construction!=null&&enabled.Count>0&&!enabled.Any(p=>
                SkillLevel(p,SkillDefOf.Construction)>=construction.constructionSkillPrerequisite&&
                SkillLevel(p,SkillDefOf.Artistic)>=construction.artisticSkillPrerequisite))
            {r.Reason="CTA_Skill";r.Confidence="CTA_Confirmed";return TaskState.Eligible;}
            if(bill!=null&&bill.recipe.skillRequirements!=null&&enabled.Count>0&&
                !enabled.Any(p=>bill.recipe.skillRequirements.All(requirement=>requirement.PawnSatisfies(p))))
            {r.Reason="CTA_Skill";r.Confidence="CTA_Confirmed";return TaskState.Eligible;}
            if(ctx.Skill!=null&&ctx.MinSkill>0&&enabled.Count>0&&!enabled.Any(p=>SkillLevel(p,ctx.Skill)>=ctx.MinSkill))
            {r.Reason="CTA_Skill";r.Confidence="CTA_Confirmed";return TaskState.Eligible;}
            if (bill != null && enabled.Count > 0 && !enabled.Any(p => bill.PawnAllowedToStartAnew(p)))
            { r.Reason = "CTA_BillRestriction"; r.Confidence = "CTA_Confirmed"; return TaskState.Eligible; }
            var available = cachedAvailable ?? enabled.Where(p => !p.Drafted && !p.Downed && !p.InMentalState && p.Awake() &&
                (bill == null || bill.PawnAllowedToStartAnew(p))).ToList();
            if(key!=null&&cachedAvailable==null)availableCache[key]=available;
            if (available.Count == 0)
            { r.Reason = "CTA_Waiting"; r.Confidence = "CTA_Uncertain"; return TaskState.Hold; }
            if (IsWorked(ctx)) { r.Reason = "CTA_Working"; r.Confidence = "CTA_Confirmed"; return TaskState.Working; }
            if (target != null)
            {
                string reservation=BlockerEvidence.Reservation(map.reservationManager.IsReserved(target),available.Any(p=>p.CanReserve(target)));
                if(reservation!=null){r.Reason=reservation;r.Confidence="CTA_Likely";return TaskState.Eligible;}
                if(construction!=null)
                {
                    Frame frame=target as Frame;
                    // Blueprints keep their stuff in stuffToUse (Thing.Stuff is null): use the
                    // game's own cost list, which applies it, instead of CostListAdjusted(null).
                    var costs=((IConstructible)target).TotalMaterialCost();
                    string material=BlockerEvidence.Materials(costs.Select(cost=>new KeyValuePair<int,int>(
                        frame!=null?frame.ThingCountNeeded(cost.thingDef):cost.count,Stock(cost.thingDef))));
                    if(material!=null){r.Reason=material;r.Confidence="CTA_Likely";return TaskState.Eligible;}
                }
            }
            if(bill!=null)
                foreach(IngredientCount ingredient in bill.recipe.ingredients)
                {
                    bool any=false,complete=true;
                    foreach(ThingDef def in ingredient.filter.AllowedThingDefs)
                    {
                        if(ingredientBudget--<=0){complete=false;PartialScan=true;break;}
                        if(!bill.ingredientFilter.Allows(def))continue;
                        if(map.listerThings.ThingsOfDef(def).Any(t=>ingredient.filter.Allows(t)&&bill.ingredientFilter.Allows(t)&&!t.IsForbidden(Faction.OfPlayer)))
                        {any=true;break;}
                    }
                    if(complete&&!any){r.Reason="CTA_BillMaterials";r.Confidence="CTA_Likely";return TaskState.Eligible;}
                }
            if(ctx.SowBlocked){r.Reason="CTA_SowBlocked";r.Confidence="CTA_Likely";return TaskState.Eligible;}
            // Interior cells of a mining area become reachable only after outer rock is mined.
            if(ctx.MineQueued){r.Reason="CTA_MineQueued";r.Confidence="CTA_Likely";return TaskState.Hold;}
            if (work == null && bill == null) return TaskState.Eligible; // Unsupported task type: do not guess workers.
            if (ctx.Zone != null) return TaskState.Eligible; // Zone tasks have no single target to path to.
            bool allChecked = available.Count <= 12;
            foreach (Pawn pawn in available.Take(12))
            {
                if (pathBudget-- <= 0) { allChecked = false; break; }
                if (pawn.CanReach(target != null ? new LocalTargetInfo(target) : new LocalTargetInfo(ctx.Cell),
                    PathEndMode.Touch, Danger.Deadly)) return TaskState.Eligible;
            }
            if (allChecked) { r.Reason = "CTA_Unreachable"; r.Confidence = "CTA_Likely"; }
            return TaskState.Eligible;
        }
        int Stock(ThingDef def)
        {
            int stock;
            if(!materialStocks.TryGetValue(def,out stock))
            {
                stock=map.listerThings.ThingsOfDef(def).Where(t=>t.Spawned&&!t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
                materialStocks[def]=stock;
            }
            return stock;
        }

        static bool MineCellQueued(IntVec3 cell, Map map)
        {
            foreach(IntVec3 offset in GenAdj.AdjacentCells)
            {
                IntVec3 next=cell+offset;
                if(next.InBounds(map)&&next.Standable(map))return false;
            }
            return true;
        }

        static int Windows(int total,int cap){return total<=cap?1:(total+cap-1)/cap;}

        static int ZoneRotationScans(List<Zone_Growing> zones)
        {
            int scans=0,i=0;
            while(i<zones.Count)
            {
                int budget=MaxZoneCellsTotal,taken=0;
                while(i<zones.Count)
                {
                    int cost=Math.Min(zones[i].CellCount,MaxObjects);
                    if(cost>budget&&taken>0)break;
                    budget-=cost;taken++;i++;
                }
                scans++;
            }
            return Math.Max(1,scans);
        }

        private void ScanCore()
        {
            AuditorSettings settings = TaskAuditorMod.Settings;
            BuildStaticMappings();
            int now = Find.TickManager.TicksGame;
            PartialScan = false;
            int failed=0;
            workers = map.mapPawns.AllPawnsSpawned.Where(IsWorker).ToList();
            activeJobs=new List<Job>();jobsByThing=new Dictionary<Thing,List<Job>>();jobsByCell=new Dictionary<IntVec3,List<Job>>();
            activeBills = new HashSet<Bill>();busyBenches=new HashSet<Thing>();
            foreach (Pawn p in workers)
            {
                Job job=p.CurJob;
                if (job == null) continue;
                activeJobs.Add(job);
                if(job.workGiverDef!=null&&job.workGiverDef.workType!=null)recentWorkTypes.Add(job.workGiverDef.workType);
                if (job.bill != null)
                {
                    activeBills.Add(job.bill);
                    Thing bench=job.bill.billStack==null?null:job.bill.billStack.billGiver as Thing;
                    if(bench!=null)busyBenches.Add(bench);
                }
                foreach (LocalTargetInfo t in new[] {job.targetA, job.targetB, job.targetC})
                {
                    if (!t.IsValid) continue;
                    List<Job> list;
                    // Thing targets are indexed by identity AND by cell: cell-based orders
                    // (mining) are performed by jobs that target the Thing on that cell.
                    if(t.HasThing){if(!jobsByThing.TryGetValue(t.Thing,out list))jobsByThing[t.Thing]=list=new List<Job>();list.Add(job);}
                    if(!jobsByCell.TryGetValue(t.Cell,out list))jobsByCell[t.Cell]=list=new List<Job>();list.Add(job);
                }
            }
            materialStocks=new Dictionary<ThingDef,int>();enabledCache.Clear();availableCache.Clear();
            ingredientBudget=512;
            pathBudget = 64; // Shared across all tasks, never per task.
            var old = new Dictionary<string, AuditRecord>();
            foreach (AuditRecord r in records)
                if (r != null && !string.IsNullOrEmpty(r.Id) && !old.ContainsKey(r.Id))
                {
                    var copy = new AuditRecord {Id=r.Id,Category=r.Category,Description=r.Description,FirstSeen=r.FirstSeen,
                        LastSeen=r.LastSeen,X=r.X,Z=r.Z,Reason=r.Reason,Confidence=r.Confidence,Held=r.Held};
                    var c=r.Clock ?? new ProgressClock();
                    copy.Clock = new ProgressClock {Initialized=c.Initialized,WasEligible=c.WasEligible,Notified=c.Notified,HasProgress=c.HasProgress,
                        FirstSeen=c.FirstSeen,LastSeen=c.LastSeen,LastProgress=c.LastProgress,Started=c.Started,
                        EligibleTicks=c.EligibleTicks,Progress=c.Progress};
                    old.Add(copy.Id,copy);
                }
            var completeCategories=new HashSet<string>(new[]{"designation","bill","grow","unfinished","gather"});
            var gaps=new Dictionary<string,int>();
            var seen = new HashSet<string>();
            var nextTargets=new Dictionary<Thing,List<AuditRecord>>();
            var nextCells=new Dictionary<IntVec3,List<AuditRecord>>();
            var nextMatchers=new Dictionary<string,Predicate<Job>>();
            var nextContexts=new Dictionary<string,TaskContext>();
            Action<string,string,string,IntVec3,float,TaskContext,bool> observe =
                (id,category,label,cell,progress,ctx,increasingOnly) =>
            {
                if (!seen.Add(id)) return;
                AuditRecord r;
                if (!old.TryGetValue(id,out r)) old[id] = r = new AuditRecord {Id=id,FirstSeen=now};
                r.Category=category; r.Description=label; r.LastSeen=now;
                if (cell.IsValid) {r.X=cell.x; r.Z=cell.z;}
                if(ctx.Target==null&&ctx.Zone==null&&ctx.Bench==null&&ctx.Cell.IsValid)
                {
                    List<AuditRecord> atCell;
                    if(!nextCells.TryGetValue(ctx.Cell,out atCell))nextCells[ctx.Cell]=atCell=new List<AuditRecord>();
                    atCell.Add(r);
                }
                if(ctx.Target!=null&&ctx.Zone==null&&ctx.Bench==null)
                {
                    List<AuditRecord> associated;
                    if(!nextTargets.TryGetValue(ctx.Target,out associated))nextTargets[ctx.Target]=associated=new List<AuditRecord>();
                    associated.Add(r);
                }
                nextMatchers[id]=ctx.Matches;nextContexts[id]=ctx;
                TaskState state=Diagnose(r,ctx);
                // Normal backlog: the same work type (or this workbench) is being worked on
                // elsewhere, so an untouched entry waits its turn. Confirmed blockers still age.
                bool backlog=state==TaskState.Eligible&&r.Confidence!="CTA_Confirmed"&&
                    (ctx.Bench!=null?busyBenches.Contains(ctx.Bench):
                    (category=="designation"||category=="grow")&&ctx.Work!=null&&recentWorkTypes.Contains(ctx.Work));
                r.Held=backlog;
                int gap;
                if(!gaps.TryGetValue(category,out gap))gap=Interval*2;
                r.Clock.Observe(now,progress,state==TaskState.Working,state!=TaskState.Paused,gap,
                    state==TaskState.Hold||backlog,increasingOnly);
            };
            Action<string,Action> guard=(category,body)=>
            {
                try{body();}
                catch(Exception e){failed++;completeCategories.Remove(category);PartialScan=true;Warn(category,e);}
            };
            if (settings.Designations)
            {
                var all=map.designationManager.AllDesignations.ToList();
                int count=Math.Min(all.Count,MaxDesignations);
                var construction=map.listerThings.ThingsInGroup(ThingRequestGroup.Construction);
                int constructionCount=Math.Min(construction.Count,MaxDesignations);
                gaps["designation"]=Interval*(Math.Max(Windows(all.Count,MaxDesignations),Windows(construction.Count,MaxDesignations))+1);
                if (count<all.Count||constructionCount<construction.Count) {PartialScan=true;completeCategories.Remove("designation");}
                for (int i=0;i<count;i++)
                {
                    Designation d=all[(designationOffset+i)%all.Count];
                    if (d == null || d.def == null) continue;
                    guard("designation",()=>
                    {
                        Thing target=d.target.HasThing ? d.target.Thing : null;
                        WorkTypeDef work=null;
                        if (d.def.defName == "Mine") work=WorkTypeDefOf.Mining;
                        if (d.def.defName == "Deconstruct") work=WorkTypeDefOf.Construction;
                        if (d.def.defName == "HarvestPlant" || d.def.defName == "CutPlant") work=WorkTypeDefOf.PlantCutting;
                        string designationLabel=string.IsNullOrEmpty(d.def.label)?d.def.defName:d.def.label;
                        string targetLabel=target!=null?target.LabelShort:d.target.Cell.ToString();
                        var ctx=new TaskContext{Target=target,Cell=d.target.Cell,Work=work,Matches=DesignationMatcher(d.def),
                            MineQueued=d.def.defName=="Mine"&&target==null&&MineCellQueued(d.target.Cell,map)};
                        observe("designation:"+d.def.defName+":"+d.target,"designation",designationLabel+" / "+targetLabel,
                            d.target.Cell,target == null ? 0 : target.HitPoints,ctx,false);
                    });
                }
                designationOffset=all.Count==0 ? 0 : (designationOffset+count)%all.Count;
                // Blueprints/frames are not designations in vanilla.
                for(int i=0;i<constructionCount;i++)
                {
                    Thing t=construction[(constructionOffset+i)%construction.Count];
                    Frame frame=t as Frame;
                    if (frame == null && !(t is Blueprint)) continue;
                    guard("designation",()=>observe("construction:"+t.ThingID,"designation",t.LabelShort,t.Position,
                        frame == null ? 0 : frame.workDone,new TaskContext{Target=t,Cell=t.Position,Work=WorkTypeDefOf.Construction,Matches=ConstructionMatcher},false));
                }
                constructionOffset=construction.Count==0?0:(constructionOffset+constructionCount)%construction.Count;
            }
            if (settings.Bills)
            {
                var bills=new List<KeyValuePair<Building,Bill>>();
                foreach (Building b in map.listerBuildings.allBuildingsColonist)
                {
                    IBillGiver giver=b as IBillGiver;
                    if (giver == null || giver.BillStack == null) continue;
                    foreach (Bill bill in giver.BillStack.Bills)
                        if (bill != null) bills.Add(new KeyValuePair<Building,Bill>(b,bill));
                }
                int count=Math.Min(bills.Count,MaxBills);
                gaps["bill"]=Interval*(Windows(bills.Count,MaxBills)+1);
                if(count<bills.Count){PartialScan=true;completeCategories.Remove("bill");}
                for(int i=0;i<count;i++)
                {
                    var entry=bills[(billOffset+i)%bills.Count];
                    guard("bill",()=>
                    {
                        Building b=entry.Key;Bill bill=entry.Value;
                        Bill_Production prod=bill as Bill_Production;
                        string paused=bill.suspended || !bill.ShouldDoNow() || (prod != null && prod.paused)?"CTA_Paused":null;
                        // Autonomous workbenches (mech gestator, ...) run one bill at a time; the
                        // others cannot start until the active one finishes.
                        var autonomous=b as Building_WorkTableAutonomous;
                        if(paused==null&&bill is Bill_Autonomous&&autonomous!=null&&autonomous.ActiveBill!=null&&autonomous.ActiveBill!=bill)paused="CTA_Queued";
                        observe("bill:"+bill.GetUniqueLoadID(),"bill",b.LabelShort+" / "+bill.Label,b.Position,
                            prod == null ? 0 : prod.repeatCount,new TaskContext{Target=b,Cell=b.Position,Bill=bill,Bench=b,PausedReason=paused},false);
                    });
                }
                billOffset=bills.Count==0?0:(billOffset+count)%bills.Count;
            }
            if (settings.Farming)
            {
                var zones=map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z=>z.CellCount>0).ToList();
                // Decide this scan's window first, so the rotation-aware gap is known before
                // any zone is observed.
                int cellBudget=MaxZoneCellsTotal,processed=0;
                for(int i=0;i<zones.Count;i++)
                {
                    int cost=Math.Min(zones[(zoneOffset+i)%zones.Count].CellCount,MaxObjects);
                    if(cost>cellBudget&&processed>0)break;
                    cellBudget-=cost;processed++;
                }
                if(processed<zones.Count){PartialScan=true;completeCategories.Remove("grow");}
                // A zone is revisited once the whole list has been rotated through. Count the
                // scans of one full budgeted rotation (zone sizes differ, so this scan's count
                // says nothing about the others) and allow one more for the window boundary.
                gaps["grow"]=Interval*(ZoneRotationScans(zones)+2);
                for(int i=0;i<processed;i++)
                {
                    Zone_Growing zone=zones[(zoneOffset+i)%zones.Count];
                    guard("grow",()=>ObserveZone(zone,observe,completeCategories));
                }
                zoneOffset=zones.Count==0?0:(zoneOffset+processed)%zones.Count;
            }
            if (settings.Unfinished)
            {
                var all=new List<Thing>();
                foreach(ThingDef def in unfinishedDefs)all.AddRange(map.listerThings.ThingsOfDef(def));
                int count=Math.Min(all.Count,MaxObjects);
                gaps["unfinished"]=Interval*(Windows(all.Count,MaxObjects)+1);
                if (count<all.Count) {PartialScan=true;completeCategories.Remove("unfinished");}
                for(int i=0;i<count;i++)
                {
                    UnfinishedThing t=all[(objectOffset+i)%all.Count] as UnfinishedThing;
                    if(t==null) continue;
                    guard("unfinished",()=>observe("unfinished:"+t.ThingID,"unfinished",t.LabelShort,t.Position,-t.workLeft,
                        new TaskContext{Target=t,Cell=t.Position,Bill=t.BoundBill,Matches=UnfinishedMatcher,
                            PausedReason=t.BoundBill!=null && (t.BoundBill.suspended || !t.BoundBill.ShouldDoNow())?"CTA_Paused":null},false));
                }
                objectOffset=all.Count==0 ? 0 : (objectOffset+count)%all.Count;
            }
            if (settings.Gathering)
            {
                var animals=map.mapPawns.SpawnedColonyAnimals;
                int count=Math.Min(animals.Count,MaxAnimals);
                gaps["gather"]=Interval*(Windows(animals.Count,MaxAnimals)+1);
                if(count<animals.Count){PartialScan=true;completeCategories.Remove("gather");}
                for(int i=0;i<count;i++)
                {
                    Pawn pawn=animals[(animalOffset+i)%animals.Count];
                    guard("gather",()=>
                    {
                        var comps=pawn.AllComps.OfType<CompHasGatherableBodyResource>().ToList();
                        foreach (CompHasGatherableBodyResource comp in comps)
                            if (comp.ActiveAndFull) observe("gather:"+pawn.ThingID+":"+comp.GetType().FullName,"gather",
                                pawn.LabelShort,pawn.Position,comp.Fullness,
                                new TaskContext{Target=pawn,Cell=pawn.Position,Work=WorkTypeDefOf.Handling,Matches=GatherMatcher(comp,comps.Count)},false);
                    });
                }
                animalOffset=animals.Count==0?0:(animalOffset+count)%animals.Count;
            }
            // A capped or failed scan cannot prove absence: unseen records keep their clocks.
            // The rotation-aware gap lets them age across windows only between two eligible,
            // unchanged observations; they never alert until observed again.
            records=old.Values.Where(r=>seen.Contains(r.Id)||!completeCategories.Contains(r.Category))
                .OrderBy(r=>r.Category).ThenBy(r=>r.FirstSeen).ToList();
            // Disabling a monitor removes its historical state even when another category is capped.
            records.RemoveAll(r => (r.Category=="designation"&&!settings.Designations) ||
                (r.Category=="bill"&&!settings.Bills) || (r.Category=="grow"&&!settings.Farming) ||
                (r.Category=="unfinished"&&!settings.Unfinished) || (r.Category=="gather"&&!settings.Gathering));
            recordLookup=records.ToDictionary(r=>r.Id);
            targetRecords=nextTargets;
            cellRecords=nextCells;
            matchers=nextMatchers;Contexts=nextContexts;
            foreach(string key in liveProgress.Keys.Where(k=>!recordLookup.ContainsKey(k)).ToList())liveProgress.Remove(key);
            recentWorkTypes.Clear();
            FailedItems=failed;
            int threshold=Math.Max(1,settings.WarningDays)*60000;
            int alerts=0;
            foreach(AuditRecord r in records)
                if (seen.Contains(r.Id) && r.Clock.IsStalled(Threshold(r)) &&
                    r.Confidence=="CTA_Confirmed" && !r.Clock.Notified && settings.Notifications)
                {r.Clock.Notified=true;alerts++;}
            if(alerts>0) Messages.Message("CTA_Notification".Translate(alerts),MessageTypeDefOf.NegativeEvent,false);
            Overdue=records.Where(r=>seen.Contains(r.Id)&&r.Clock.ReachedThreshold(Threshold(r))).ToList();
            LastScanTick=now;ProofBudget=64;
            map.GetComponent<TaskActionState>().Prune(records.Select(r=>r.Id),!PartialScan);
            if(!PartialScan&&settings.AssistanceMode!=ActionMode.Off)
            {
                TemporaryWorkAssistance.PublishReadyFacts(this);
                int ready=0;
                foreach(var watch in map.GetComponent<TaskActionState>().Watches.Take(4))
                {
                    var record=FindRecord(watch.Info.Id);
                    if(record!=null&&record.Reason=="CTA_Ready"&&!record.Clock.Notified&&settings.Notifications)
                    {record.Clock.Notified=true;ready++;}
                }
                if(ready>0)Messages.Message("CTA_Notification".Translate(ready),MessageTypeDefOf.NegativeEvent,false);
            }
        }

        private void ObserveZone(Zone_Growing zone,Action<string,string,string,IntVec3,float,TaskContext,bool> observe,HashSet<string> completeCategories)
        {
            int empty=0,blocked=0,harvest=0,checkedCells=0;
            IntVec3 sowCell=IntVec3.Invalid,harvestCell=IntVec3.Invalid;
            ThingDef plantDef=zone.GetPlantDefToGrow();
            if(plantDef==null||plantDef.plant==null)return;
            bool paused=!zone.allowSow || !zone.CanAcceptSowNow();
            foreach (IntVec3 cell in zone.Cells)
            {
                if (++checkedCells>MaxObjects) {PartialScan=true;completeCategories.Remove("grow");break;}
                Plant plant=cell.GetPlant(map);
                if (plant==null&&cell.Standable(map)&&map.fertilityGrid.FertilityAt(cell)>=plantDef.plant.fertilityMin)
                {
                    empty++;
                    if(cell.GetFirstItem(map)!=null)blocked++;
                    else if(!sowCell.IsValid)sowCell=cell;
                }
                else if (plant!=null&&plant.HarvestableNow)
                {harvest++;if(!harvestCell.IsValid)harvestCell=cell;}
            }
            if(!sowCell.IsValid&&empty>0)sowCell=zone.Cells[0];
            // Pending counts are encoded as -count: only a decrease of pending work is progress.
            if (empty>0) observe("grow:"+zone.ID,"grow",zone.label+" / "+"CTA_Empty".Translate(empty)+(blocked>0?" "+"CTA_EmptyBlocked".Translate(blocked).ToString():""),
                sowCell,-empty,new TaskContext{Cell=sowCell,Zone=zone,Work=WorkTypeDefOf.Growing,Matches=SowMatcher,
                    PausedReason=paused?"CTA_Paused":null,Skill=SkillDefOf.Plants,MinSkill=plantDef.plant.sowMinSkill,
                    SowBlocked=blocked==empty},true);
            if (harvest>0) observe("harvest:"+zone.ID,"grow",zone.label+" / "+"CTA_Harvest".Translate(harvest),
                harvestCell,-harvest,new TaskContext{Target=harvestCell.GetPlant(map),Cell=harvestCell,Zone=zone,Work=WorkTypeDefOf.Growing,
                    Matches=HarvestMatcher,PausedReason=!zone.allowCut?"CTA_Paused":null},true);
        }
    }
}
