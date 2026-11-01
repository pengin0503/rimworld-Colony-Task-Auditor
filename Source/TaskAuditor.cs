using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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
        public bool Notified;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id", "");
            Scribe_Values.Look(ref Category, "category", "");
            Scribe_Values.Look(ref Description, "description", "");
            Scribe_Values.Look(ref FirstSeen, "firstSeen", 0);
            Scribe_Values.Look(ref LastSeen, "lastSeen", 0);
            Scribe_Values.Look(ref X, "x", -1);
            Scribe_Values.Look(ref Z, "z", -1);
            Scribe_Values.Look(ref Notified, "notified", false);
        }
    }

    public sealed class TaskAuditComponent : MapComponent
    {
        private const int Interval = 5000;
        private const int MaxDesignations = 5000;
        private const int MaxObjects = 12000;
        private List<AuditRecord> records = new List<AuditRecord>();
        public IReadOnlyList<AuditRecord> Records { get { return records; } }
        public int LastScanTick { get; private set; }
        public bool PartialScan { get; private set; }

        public TaskAuditComponent(Map map) : base(map) { }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "auditRecords", LookMode.Deep);
            if (records == null) records = new List<AuditRecord>();
        }
        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval == map.uniqueID % Interval) Scan();
        }
        public void Scan()
        {
            AuditorSettings settings = TaskAuditorMod.Settings;
            if (settings == null || !map.IsPlayerHome) return;
            int now = Find.TickManager.TicksGame;
            PartialScan = false;
            var old = new Dictionary<string, AuditRecord>();
            foreach (AuditRecord item in records)
                if (item != null && !string.IsNullOrEmpty(item.Id) && !old.ContainsKey(item.Id))
                    old.Add(item.Id, item);
            var seen = new HashSet<string>();
            Action<string, string, string, IntVec3> observe = (id, category, label, cell) =>
            {
                if (!seen.Add(id)) return;
                AuditRecord record;
                if (!old.TryGetValue(id, out record))
                    old[id] = record = new AuditRecord { Id = id, FirstSeen = now };
                record.Category = category;
                record.Description = label;
                record.LastSeen = now;
                if (cell.IsValid) { record.X = cell.x; record.Z = cell.z; }
            };

            if (settings.Designations)
            {
                int scanned = 0;
                foreach (Designation d in map.designationManager.AllDesignations)
                {
                    if (++scanned > MaxDesignations) { PartialScan = true; break; }
                    if (d == null || d.def == null) continue;
                    observe("designation:" + d.def.defName + ":" + d.target,
                        "designation", d.def.label + " / " + d.target, d.target.Cell);
                }
            }
            if (settings.Bills)
            {
                // Build one active-bill set; avoid O(bills * pawns) scanning.
                var active = new HashSet<Bill>();
                foreach (Pawn worker in map.mapPawns.AllPawnsSpawned)
                    if (worker.CurJob != null && worker.CurJob.bill != null)
                        active.Add(worker.CurJob.bill);
                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    IBillGiver giver = building as IBillGiver;
                    if (giver == null || giver.BillStack == null) continue;
                    foreach (Bill bill in giver.BillStack.Bills)
                    {
                        if (bill == null || bill.suspended || !bill.ShouldDoNow() || active.Contains(bill)) continue;
                        Bill_Production prod = bill as Bill_Production;
                        if (prod != null && prod.paused) continue;
                        observe("bill:" + bill.GetUniqueLoadID(), "bill",
                            building.LabelShort + " / " + bill.Label, building.Position);
                    }
                }
            }
            if (settings.Farming)
            {
                foreach (Zone_Growing zone in map.zoneManager.AllZones.OfType<Zone_Growing>())
                {
                    if (!zone.allowSow || !zone.CanAcceptSowNow() || zone.CellCount == 0) continue;
                    int empty = 0, checkedCells = 0;
                    foreach (IntVec3 cell in zone.Cells)
                    {
                        if (++checkedCells > MaxObjects) { PartialScan = true; break; }
                        if (cell.GetPlant(map) == null) empty++;
                    }
                    if (empty > 0)
                        observe("grow:" + zone.ID, "grow",
                            zone.label + " / " + "CTA_Empty".Translate(empty), zone.Position);
                }
            }
            if (settings.Unfinished)
            {
                int scanned = 0;
                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (++scanned > MaxObjects) { PartialScan = true; break; }
                    UnfinishedThing item = thing as UnfinishedThing;
                    if (item != null && (item.BoundBill == null || item.BoundWorkTable == null))
                        observe("unfinished:" + item.ThingID, "unfinished",
                            item.LabelShort + " / " + "CTA_Missing".Translate(), item.Position);
                }
            }
            if (settings.Gathering)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!pawn.IsColonyAnimal) continue;
                    foreach (CompHasGatherableBodyResource comp in pawn.AllComps.OfType<CompHasGatherableBodyResource>())
                        if (comp.ActiveAndFull)
                            observe("gather:" + pawn.ThingID + ":" + comp.GetType().FullName, "gather",
                                pawn.LabelShort + " / " + comp.GetType().Name, pawn.Position);
                }
            }

            // Never keep records for completed or disabled tasks. FirstSeen survives save/load.
            records = old.Values.Where(r => seen.Contains(r.Id))
                .OrderBy(r => r.Category).ThenBy(r => r.FirstSeen).Take(2000).ToList();
            int newAlerts = 0;
            foreach (AuditRecord r in records)
            {
                if (!r.Notified && now - r.FirstSeen >= settings.WarningDays * 60000)
                {
                    r.Notified = true;
                    newAlerts++;
                }
            }
            if (newAlerts > 0 && settings.Notifications)
                Messages.Message("CTA_Notification".Translate(newAlerts), MessageTypeDefOf.NegativeEvent, false);
            LastScanTick = now;
        }
    }

    public sealed class TaskAuditorMod : Mod
    {
        public static AuditorSettings Settings;
        private Vector2 scroll;
        public TaskAuditorMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<AuditorSettings>();
        }
        public override string SettingsCategory() { return "Colony Task Auditor"; }
        public override void DoSettingsWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(new Rect(rect.x, rect.y, rect.width, 287f));
            list.Label("CTA_Description".Translate());
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
            int now = Find.TickManager.TicksGame;
            int threshold = Settings.WarningDays * 60000;
            int overdueCount = audit.Records.Count(r => now - r.FirstSeen >= threshold);
            Widgets.Label(new Rect(rect.x + 170, rect.y + 299, rect.width - 171, 28),
                "CTA_Count".Translate(audit.Records.Count, overdueCount));

            // Overdue items are sorted first so users see urgent issues without paging.
            List<AuditRecord> display = audit.Records
                .OrderBy(r => now - r.FirstSeen < threshold)
                .ThenBy(r => r.FirstSeen).Take(75).ToList();
            Rect viewport = new Rect(rect.x, rect.y + 337, rect.width, Math.Max(75f, rect.height - 340f));
            Rect content = new Rect(0, 0, Math.Max(100f, viewport.width - 20), Math.Max(viewport.height, display.Count * 48f));
            Widgets.BeginScrollView(viewport, ref scroll, content);
            for (int i = 0; i < display.Count; i++)
            {
                AuditRecord item = display[i];
                bool overdue = now - item.FirstSeen >= threshold;
                Widgets.Label(new Rect(0, i * 48f, content.width - 80f, 45f),
                    (overdue ? "CTA_Overdue".Translate() : "CTA_Watching".Translate()) +
                    " [" + item.Category + "] " + item.Description + "\n" +
                    "CTA_Days".Translate(((now - item.FirstSeen) / 60000f).ToString("0.0")));
                if (item.X >= 0 &&
                    Widgets.ButtonText(new Rect(content.width - 76f, i * 48f + 5, 73f, 28f), "CTA_Jump".Translate()))
                    CameraJumper.TryJump(new IntVec3(item.X, 0, item.Z), current);
            }
            Widgets.EndScrollView();
            if (audit.PartialScan)
                Widgets.Label(new Rect(rect.x, rect.y + rect.height - 24, rect.width, 22), "CTA_Partial".Translate());
        }
    }
}
