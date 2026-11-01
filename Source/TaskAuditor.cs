using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Diagnostics;

namespace Pengin.ColonyTaskAuditor
{
    public sealed class AuditorSettings : ModSettings
    {
        public int WarningDays = 2;
        public bool Designations = true, Bills = true, Farming = true, Unfinished = true, Gathering = true;
        public bool Notifications = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref WarningDays, "warningDays", 2);
            Scribe_Values.Look(ref Designations, "designations", true);
            Scribe_Values.Look(ref Bills, "bills", true);
            Scribe_Values.Look(ref Farming, "farming", true);
            Scribe_Values.Look(ref Unfinished, "unfinished", true);
            Scribe_Values.Look(ref Gathering, "gathering", true);
            Scribe_Values.Look(ref Notifications, "notifications", true);
            base.ExposeData();
        }
    }

    public sealed class AuditRecord : IExposable
    {
        public string Id, Category, Description;
        public int FirstSeen, LastSeen, X = -1, Z = -1;
        public ProgressClock Clock = new ProgressClock();
        public string Reason = "CTA_Unknown", Confidence = "CTA_Uncertain";
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

    public sealed class TaskAuditComponent : MapComponent
    {
        private const int Interval = 5000;
        private const int MaxDesignations = 5000, MaxObjects = 12000;
        private List<AuditRecord> records = new List<AuditRecord>();
        public IReadOnlyList<AuditRecord> Records { get { return records; } }
        public int LastScanTick { get; private set; }
        public bool PartialScan { get; private set; }
        public double ScanMilliseconds { get; private set; }
        private int designationOffset, objectOffset,constructionOffset;
        private HashSet<Thing> activeThings;
        private HashSet<IntVec3> activeCells;
        private HashSet<Bill> activeBills;
        private List<Pawn> colonists;
        private int pathBudget,ingredientBudget;
        private Dictionary<string,AuditRecord> recordLookup=new Dictionary<string,AuditRecord>();
        private bool needsInitialScan=true;
        private Dictionary<Thing,List<AuditRecord>> targetRecords=new Dictionary<Thing,List<AuditRecord>>();
        private Dictionary<string,float> liveProgress=new Dictionary<string,float>();
        public void NotifyBillProgress(Bill bill)
        {
            AuditRecord record;
            if(recordLookup.TryGetValue("bill:"+bill.GetUniqueLoadID(),out record))record.Clock.MarkProgress(Find.TickManager.TicksGame);
        }
        private void ObserveActiveWork()
        {
            foreach(Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if(!pawn.IsColonistPlayerControlled||pawn.CurJob==null)continue;
                int now=Find.TickManager.TicksGame;
                if(pawn.CurJob.bill!=null)
                {
                    string id="bill:"+pawn.CurJob.bill.GetUniqueLoadID();AuditRecord r;
                    if(recordLookup.TryGetValue(id,out r))
                    {
                        if(r.Clock.Started<0)r.Clock.Started=now;
                        r.Clock.EligibleTicks=0;r.Clock.WasEligible=false;
                        JobDriver_DoBill driver=pawn.jobs.curDriver as JobDriver_DoBill;
                        if(driver!=null)
                        {
                            float previous;
                            if(liveProgress.TryGetValue(id,out previous)&&previous!=driver.workLeft)r.Clock.MarkProgress(now);
                            liveProgress[id]=driver.workLeft;
                        }
                    }
                }
                foreach(LocalTargetInfo target in new[]{pawn.CurJob.targetA,pawn.CurJob.targetB,pawn.CurJob.targetC})
                {
                    if(!target.HasThing)continue;
                    List<AuditRecord> matches;
                    if(targetRecords.TryGetValue(target.Thing,out matches))
                        foreach(AuditRecord r in matches)
                        {
                            if(r.Clock.Started<0)r.Clock.Started=now;
                            r.Clock.EligibleTicks=0;r.Clock.WasEligible=false;
                            Frame frame=target.Thing as Frame;UnfinishedThing unfinished=target.Thing as UnfinishedThing;
                            float value=frame!=null?frame.workDone:unfinished!=null?-unfinished.workLeft:target.Thing.HitPoints;
                            if(value!=r.Clock.Progress){r.Clock.MarkProgress(now);r.Clock.Progress=value;}
                        }
                }
            }
        }
        private Dictionary<ThingDef,int> materialStocks;
        private Dictionary<ThingDef,HashSet<WorkTypeDef>> workMappings;
        private void BuildWorkMappings()
        {
            if(workMappings!=null)return;
            workMappings=new Dictionary<ThingDef,HashSet<WorkTypeDef>>();
            foreach(WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if(giver.workType==null||giver.fixedBillGiverDefs==null||giver.giverClass==null||!typeof(WorkGiver_DoBill).IsAssignableFrom(giver.giverClass))continue;
                foreach(ThingDef def in giver.fixedBillGiverDefs)
                {
                    HashSet<WorkTypeDef> types;
                    if(!workMappings.TryGetValue(def,out types))workMappings[def]=types=new HashSet<WorkTypeDef>();
                    types.Add(giver.workType);
                }
            }
        }

        public TaskAuditComponent(Map map) : base(map) { }
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
                Log.Warning("Colony Task Auditor scan incomplete: " + e.GetType().Name);
            }
            finally { ScanMilliseconds = timer.Elapsed.TotalMilliseconds; }
        }
        private void Diagnose(AuditRecord r, Thing target, IntVec3 cell, WorkTypeDef work, Bill bill, bool paused)
        {
            r.Reason = "CTA_Unknown"; r.Confidence = "CTA_Uncertain";
            if (paused) { r.Reason = "CTA_Paused"; r.Confidence = "CTA_Confirmed"; return; }
            if (target != null && target.IsForbidden(Faction.OfPlayer))
            { r.Reason = "CTA_Forbidden"; r.Confidence = "CTA_Confirmed"; return; }
            if(bill!=null&&work==null)work=bill.recipe.requiredGiverWorkType;
            HashSet<WorkTypeDef> applicable=null;
            if(bill!=null&&work==null&&target!=null)workMappings.TryGetValue(target.def,out applicable);
            var enabled = colonists.Where(p => p.workSettings != null &&
                (work!=null?p.workSettings.WorkIsActive(work):applicable!=null&&applicable.Count>0?applicable.Any(p.workSettings.WorkIsActive):true)).ToList();
            if (enabled.Count == 0 && (work != null||applicable!=null&&applicable.Count>0))
            { r.Reason = "CTA_NoWorker"; r.Confidence = "CTA_Confirmed"; return; }
            BuildableDef construction=target==null?null:target.def.entityDefToBuild;
            if(construction!=null&&enabled.Count>0&&!enabled.Any(p=>p.skills!=null&&
                p.skills.GetSkill(SkillDefOf.Construction).Level>=construction.constructionSkillPrerequisite&&
                p.skills.GetSkill(SkillDefOf.Artistic).Level>=construction.artisticSkillPrerequisite))
            {r.Reason="CTA_Skill";r.Confidence="CTA_Confirmed";return;}
            if(bill!=null&&bill.recipe.skillRequirements!=null&&enabled.Count>0&&
                !enabled.Any(p=>bill.recipe.skillRequirements.All(requirement=>requirement.PawnSatisfies(p))))
            {r.Reason="CTA_Skill";r.Confidence="CTA_Confirmed";return;}
            if (bill != null && enabled.Count > 0 && !enabled.Any(p => bill.PawnAllowedToStartAnew(p)))
            { r.Reason = "CTA_BillRestriction"; r.Confidence = "CTA_Confirmed"; return; }
            var available = enabled.Where(p => !p.Drafted && !p.Downed && !p.InMentalState && p.Awake() &&
                (bill == null || bill.PawnAllowedToStartAnew(p))).ToList();
            if (available.Count == 0)
            { r.Reason = "CTA_Waiting"; r.Confidence = "CTA_Uncertain"; return; }
            bool working = (target != null && activeThings.Contains(target)) || activeCells.Contains(cell) ||
                (bill != null && activeBills.Contains(bill));
            if (working) { r.Reason = "CTA_Working"; r.Confidence = "CTA_Confirmed"; return; }
            if (target != null)
            {
                string reservation=BlockerEvidence.Reservation(map.reservationManager.IsReserved(target),available.Any(p=>p.CanReserve(target)));
                if(reservation!=null){r.Reason=reservation;r.Confidence="CTA_Likely";return;}
                Frame frame=target as Frame;
                if(frame!=null||target is Blueprint)
                    foreach(ThingDefCountClass cost in frame!=null?frame.TotalMaterialCost():construction.CostListAdjusted(target.Stuff))
                    {
                        int needed=frame!=null?frame.ThingCountNeeded(cost.thingDef):cost.count;
                        if(needed<=0)continue;
                        int stock;
                        if(!materialStocks.TryGetValue(cost.thingDef,out stock))
                        {
                            stock=map.listerThings.ThingsOfDef(cost.thingDef).Where(t=>t.Spawned&&!t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
                            materialStocks[cost.thingDef]=stock;
                        }
                        r.Reason=BlockerEvidence.Materials(needed,stock);r.Confidence="CTA_Likely";return;
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
                    if(complete&&!any){r.Reason="CTA_BillMaterials";r.Confidence="CTA_Likely";return;}
                }
            if (work == null && bill == null) return; // Unsupported task type: do not guess workers.
            bool allChecked = available.Count <= 12;
            foreach (Pawn pawn in available.Take(12))
            {
                if (pathBudget-- <= 0) { allChecked = false; break; }
                if (pawn.CanReach(target != null ? new LocalTargetInfo(target) : new LocalTargetInfo(cell),
                    PathEndMode.Touch, Danger.Deadly)) return;
            }
            if (allChecked) { r.Reason = "CTA_Unreachable"; r.Confidence = "CTA_Likely"; }
        }
        private void ScanCore()
        {
            AuditorSettings settings = TaskAuditorMod.Settings;
            BuildWorkMappings();
            int now = Find.TickManager.TicksGame;
            PartialScan = false;
            colonists = map.mapPawns.AllPawnsSpawned.Where(p => p.IsColonistPlayerControlled).ToList();
            activeThings = new HashSet<Thing>(); activeCells = new HashSet<IntVec3>(); activeBills = new HashSet<Bill>();
            foreach (Pawn p in colonists)
            {
                if (p.CurJob == null) continue;
                if (p.CurJob.bill != null) activeBills.Add(p.CurJob.bill);
                foreach (LocalTargetInfo t in new[] {p.CurJob.targetA, p.CurJob.targetB, p.CurJob.targetC})
                    if (t.IsValid) { activeCells.Add(t.Cell); if (t.HasThing) activeThings.Add(t.Thing); }
            }
            materialStocks=new Dictionary<ThingDef,int>();
            ingredientBudget=512;
            pathBudget = 64; // Shared across all tasks, never per task.
            var old = new Dictionary<string, AuditRecord>();
            foreach (AuditRecord r in records)
                if (r != null && !string.IsNullOrEmpty(r.Id) && !old.ContainsKey(r.Id))
                {
                    var copy = new AuditRecord {Id=r.Id,Category=r.Category,Description=r.Description,FirstSeen=r.FirstSeen,
                        LastSeen=r.LastSeen,X=r.X,Z=r.Z,Reason=r.Reason,Confidence=r.Confidence};
                    var c=r.Clock ?? new ProgressClock();
                    copy.Clock = new ProgressClock {Initialized=c.Initialized,WasEligible=c.WasEligible,Notified=c.Notified,HasProgress=c.HasProgress,
                        FirstSeen=c.FirstSeen,LastSeen=c.LastSeen,LastProgress=c.LastProgress,Started=c.Started,
                        EligibleTicks=c.EligibleTicks,Progress=c.Progress};
                    old.Add(copy.Id,copy);
                }
            var completeCategories=new HashSet<string>(new[]{"designation","bill","grow","unfinished","gather"});
            var seen = new HashSet<string>();
            var nextTargets=new Dictionary<Thing,List<AuditRecord>>();
            Action<string,string,string,IntVec3,float,Thing,WorkTypeDef,Bill,bool> observe =
                (id,category,label,cell,progress,target,work,bill,paused) =>
            {
                if (!seen.Add(id)) return;
                AuditRecord r;
                if (!old.TryGetValue(id,out r)) old[id] = r = new AuditRecord {Id=id,FirstSeen=now};
                r.Category=category; r.Description=label; r.LastSeen=now;
                if (cell.IsValid) {r.X=cell.x; r.Z=cell.z;}
                if(target!=null)
                {
                    List<AuditRecord> associated;
                    if(!nextTargets.TryGetValue(target,out associated))nextTargets[target]=associated=new List<AuditRecord>();
                    associated.Add(r);
                }
                Diagnose(r,target,cell,work,bill,paused);
                bool working = r.Reason == "CTA_Working";
                bool eligible = r.Reason != "CTA_Paused" && r.Reason != "CTA_Waiting";
                r.Clock.Observe(now,progress,working,eligible,Interval*2);
            };
            if (settings.Designations)
            {
                var all=map.designationManager.AllDesignations.ToList();
                int count=Math.Min(all.Count,MaxDesignations);
                if (count<all.Count) {PartialScan=true;completeCategories.Remove("designation");}
                for (int i=0;i<count;i++)
                {
                    Designation d=all[(designationOffset+i)%all.Count];
                    if (d == null || d.def == null) continue;
                    Thing target=d.target.HasThing ? d.target.Thing : null;
                    WorkTypeDef work=null;
                    if (d.def.defName == "Mine") work=WorkTypeDefOf.Mining;
                    if (d.def.defName == "Deconstruct") work=WorkTypeDefOf.Construction;
                    if (d.def.defName == "HarvestPlant" || d.def.defName == "CutPlant") work=WorkTypeDefOf.PlantCutting;
                    observe("designation:"+d.def.defName+":"+d.target,"designation",d.def.label+" / "+d.target,
                        d.target.Cell,target == null ? 0 : target.HitPoints,target,work,null,false);
                }
                designationOffset=all.Count==0 ? 0 : (designationOffset+count)%all.Count;
                // Blueprints/frames are not designations in vanilla.
                var construction=map.listerThings.ThingsInGroup(ThingRequestGroup.Construction);
                int constructionCount=Math.Min(construction.Count,MaxDesignations);
                if(constructionCount<construction.Count){PartialScan=true;completeCategories.Remove("designation");}
                for(int i=0;i<constructionCount;i++)
                {
                    Thing t=construction[(constructionOffset+i)%construction.Count];
                    Frame frame=t as Frame;
                    if (frame != null || t is Blueprint) observe("construction:"+t.ThingID,"designation",t.LabelShort,t.Position,
                        frame == null ? 0 : frame.workDone,t,WorkTypeDefOf.Construction,null,false);
                }
                constructionOffset=construction.Count==0?0:(constructionOffset+constructionCount)%construction.Count;
            }
            if (settings.Bills)
                foreach (Building b in map.listerBuildings.allBuildingsColonist)
                {
                    IBillGiver giver=b as IBillGiver;
                    if (giver == null || giver.BillStack == null) continue;
                    foreach (Bill bill in giver.BillStack.Bills)
                    {
                        if (bill == null) continue;
                        Bill_Production prod=bill as Bill_Production;
                        bool paused=bill.suspended || !bill.ShouldDoNow() || (prod != null && prod.paused);
                        observe("bill:"+bill.GetUniqueLoadID(),"bill",b.LabelShort+" / "+bill.Label,b.Position,
                            prod == null ? 0 : prod.repeatCount,b,null,bill,paused);
                    }
                }
            if (settings.Farming)
                foreach (Zone_Growing zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
                {
                    if (zone.CellCount==0) continue;
                    int empty=0,harvest=0,checkedCells=0;
                    IntVec3 sowCell=IntVec3.Invalid,harvestCell=IntVec3.Invalid;
                    bool paused=!zone.allowSow || !zone.CanAcceptSowNow();
                    foreach (IntVec3 cell in zone.Cells)
                    {
                        if (++checkedCells>MaxObjects) {PartialScan=true;completeCategories.Remove("grow");break;}
                        Plant plant=cell.GetPlant(map);
                        if (plant==null&&cell.Standable(map)&&map.fertilityGrid.FertilityAt(cell)>=zone.GetPlantDefToGrow().plant.fertilityMin)
                        {empty++;if(!sowCell.IsValid)sowCell=cell;}
                        else if (plant!=null&&plant.HarvestableNow)
                        {harvest++;if(!harvestCell.IsValid)harvestCell=cell;}
                    }
                    if (empty>0) observe("grow:"+zone.ID,"grow",zone.label+" / "+"CTA_Empty".Translate(empty),
                        sowCell,-empty,null,WorkTypeDefOf.Growing,null,paused);
                    if (harvest>0) observe("harvest:"+zone.ID,"grow",zone.label+" / "+"CTA_Harvest".Translate(harvest),
                        harvestCell,-harvest,harvestCell.GetPlant(map),WorkTypeDefOf.Growing,null,!zone.allowCut);
                }
            if (settings.Unfinished)
            {
                var all=map.listerThings.AllThings;
                int count=Math.Min(all.Count,MaxObjects);
                if (count<all.Count) {PartialScan=true;completeCategories.Remove("unfinished");}
                for(int i=0;i<count;i++)
                {
                    UnfinishedThing t=all[(objectOffset+i)%all.Count] as UnfinishedThing;
                    if(t==null) continue;
                    observe("unfinished:"+t.ThingID,"unfinished",t.LabelShort,t.Position,-t.workLeft,t,null,t.BoundBill,
                        t.BoundBill!=null && (t.BoundBill.suspended || !t.BoundBill.ShouldDoNow()));
                }
                objectOffset=all.Count==0 ? 0 : (objectOffset+count)%all.Count;
            }
            if (settings.Gathering)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!pawn.IsColonyAnimal) continue;
                    foreach (CompHasGatherableBodyResource comp in pawn.AllComps.OfType<CompHasGatherableBodyResource>())
                        if (comp.ActiveAndFull) observe("gather:"+pawn.ThingID+":"+comp.GetType().FullName,"gather",
                            pawn.LabelShort,pawn.Position,comp.Fullness,pawn,WorkTypeDefOf.Handling,null,false);
                }
            // A capped scan cannot prove absence. Retain unseen records, but reset their
            // eligibility so stale diagnostics cannot produce alerts.
            foreach (AuditRecord r in old.Values)
                if (!seen.Contains(r.Id) && PartialScan) {r.Clock.WasEligible=false;r.Clock.EligibleTicks=0;}
            records=old.Values.Where(r=>seen.Contains(r.Id)||!completeCategories.Contains(r.Category))
                .OrderBy(r=>r.Category).ThenBy(r=>r.FirstSeen).ToList();
            // Disabling a monitor removes its historical state even when another category is capped.
            records.RemoveAll(r => (r.Category=="designation"&&!settings.Designations) ||
                (r.Category=="bill"&&!settings.Bills) || (r.Category=="grow"&&!settings.Farming) ||
                (r.Category=="unfinished"&&!settings.Unfinished) || (r.Category=="gather"&&!settings.Gathering));
            recordLookup=records.ToDictionary(r=>r.Id);
            targetRecords=nextTargets;
            foreach(string key in liveProgress.Keys.Where(k=>!recordLookup.ContainsKey(k)).ToList())liveProgress.Remove(key);
            int alerts=0;
            foreach(AuditRecord r in records)
                if (seen.Contains(r.Id) && r.Clock.IsStalled(Math.Max(1,settings.WarningDays)*60000) &&
                    r.Confidence=="CTA_Confirmed" && !r.Clock.Notified && settings.Notifications)
                {r.Clock.Notified=true;alerts++;}
            if(alerts>0) Messages.Message("CTA_Notification".Translate(alerts),MessageTypeDefOf.NegativeEvent,false);
            LastScanTick=now;
        }
    }

    public sealed class TaskAuditorMod : Mod
    {
        public static AuditorSettings Settings;
        private Vector2 scroll;
        private int filter;
        private static readonly string[] categories = {"all","designation","bill","grow","unfinished","gather"};
        public TaskAuditorMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<AuditorSettings>();
            BillProgressHooks.Install();
        }
        public override string SettingsCategory() { return "Colony Task Auditor"; }
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(new Rect(rect.x, rect.y, rect.width, 287f));
            list.Label("CTA_Description".Translate());
            if(!BillProgressHooks.Available)list.Label("CTA_ProgressHookUnavailable".Translate());
            list.Label("CTA_Threshold".Translate(Settings.WarningDays));
            Settings.WarningDays = (int)list.Slider(Settings.WarningDays, 1, 10);
            list.CheckboxLabeled("CTA_Designations".Translate(), ref Settings.Designations);
            list.CheckboxLabeled("CTA_Bills".Translate(), ref Settings.Bills);
            list.CheckboxLabeled("CTA_Growing".Translate(), ref Settings.Farming);
            list.CheckboxLabeled("CTA_Unfinished".Translate(), ref Settings.Unfinished);
            list.CheckboxLabeled("CTA_Gathering".Translate(), ref Settings.Gathering);
            list.CheckboxLabeled("CTA_Notifications".Translate(), ref Settings.Notifications);
            list.End();

            Map current = Find.CurrentMap;
            if (current == null) return;
            TaskAuditComponent audit = current.GetComponent<TaskAuditComponent>();
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 296, 165, 30), "CTA_Rescan".Translate()))
                audit.Scan();
            if (Widgets.ButtonText(new Rect(rect.x+rect.width-155,rect.y+296,150,30), ("CTA_Filter"+categories[filter]).Translate()))
                filter=(filter+1)%categories.Length;
            int now = Find.TickManager.TicksGame;
            int threshold = Settings.WarningDays * 60000;
            int overdueCount = audit.Records.Count(r => r.Clock.IsStalled(threshold));
            Widgets.Label(new Rect(rect.x + 170, rect.y + 299, Math.Max(50f,rect.width - 335), 28),
                "CTA_Count".Translate(audit.Records.Count, overdueCount));

            // Overdue items are sorted first so users see urgent issues without paging.
            List<AuditRecord> display = audit.Records.Where(r=>filter==0 || r.Category==categories[filter])
                .OrderBy(r => !r.Clock.IsStalled(threshold))
                .ThenBy(r => r.FirstSeen).ToList();
            Rect viewport = new Rect(rect.x, rect.y + 337, rect.width, Math.Max(75f, rect.height - 340f));
            Rect content = new Rect(0, 0, Math.Max(100f, viewport.width - 20), Math.Max(viewport.height, display.Count * 108f));
            Widgets.BeginScrollView(viewport, ref scroll, content);
            for (int i = 0; i < display.Count; i++)
            {
                if(i*108f+108<scroll.y||i*108f>scroll.y+viewport.height)continue;
                AuditRecord item = display[i];
                bool overdue = item.Clock.IsStalled(threshold);
                Widgets.Label(new Rect(0, i * 108f, content.width - 80f, 105f),
                    (overdue ? "CTA_Overdue".Translate() : "CTA_Watching".Translate()) +
                    " [" + ("CTA_Filter"+item.Category).Translate() + "] " + item.Description + "\n" +
                    "CTA_Days".Translate((item.Clock.EligibleTicks / 60000f).ToString("0.0")) + " / " + item.Reason.Translate() + " (" + item.Confidence.Translate() + ")" + "\n" +
                    "CTA_Timing".Translate(item.FirstSeen,item.Clock.Started<0?"—":item.Clock.Started.ToString(),item.Clock.HasProgress?item.Clock.LastProgress.ToString():"—"));
                if (item.X >= 0 &&
                    Widgets.ButtonText(new Rect(content.width - 76f, i * 108f + 5, 73f, 28f), "CTA_Jump".Translate()))
                    CameraJumper.TryJump(new IntVec3(item.X, 0, item.Z), current);
            }
            Widgets.EndScrollView();
            if (audit.PartialScan)
                Widgets.Label(new Rect(rect.x, rect.y + rect.height - 24, rect.width, 22), "CTA_Partial".Translate());
        }
    }
}

