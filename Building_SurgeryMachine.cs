using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    public class Building_SurgeryMachine : Building, IThingHolder
    {
        private ThingOwner<Pawn> pawnContainer;

        private CompPowerTrader PowerComp => this.TryGetComp<CompPowerTrader>();
        private CompFlickable FlickComp => this.TryGetComp<CompFlickable>();

        public Pawn PawnInside => (pawnContainer != null && pawnContainer.Count > 0) ? pawnContainer[0] : null;

        // Alleen entry/carry power-gated. Eject + items altijd toegestaan.
        public bool CanOperateNow
        {
            get
            {
                bool hasPower = PowerComp != null && PowerComp.PowerOn;
                bool isOn = FlickComp == null || FlickComp.SwitchIsOn;
                return hasPower && isOn;
            }
        }

        // --- “Soft stasis” tuning ---
        // Hunger tikt door, rest/joy pauze, health ticks pauze.
        private const int HungerIntervalTicks = 150;           // vanilla-ish interval
        private const float MalnutritionPerInterval = 0.0125f; // voelt “logisch”, kan je later tunen
        private HediffDef cachedMalnutritionDef;

        public Building_SurgeryMachine()
        {
            pawnContainer = new ThingOwner<Pawn>(this, oneStackOnly: true);
        }

        // --- IThingHolder ---
        public ThingOwner GetDirectlyHeldThings() => pawnContainer;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        // --- Save/Load ---
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref pawnContainer, "pawnContainer", this);

            if (pawnContainer == null)
                pawnContainer = new ThingOwner<Pawn>(this, oneStackOnly: true);
        }

        // --- Inventory comp ---
        public Comp_SurgeryMachineInventory InventoryComp => this.TryGetComp<Comp_SurgeryMachineInventory>();

        // --- Accept rules (pawn entry/carry) ---
        public bool CanAcceptPawnNow(Pawn p, out string reason)
        {
            reason = null;

            if (!CanOperateNow) { reason = "No power / switched off."; return false; }
            if (PawnInside != null) { reason = "Machine is occupied."; return false; }
            if (p == null) { reason = "No pawn."; return false; }
            if (!p.Spawned) { reason = "Pawn is not spawned."; return false; }
            if (p.Dead) { reason = "Pawn is dead."; return false; }

            return true;
        }

        public bool TryAcceptPawn(Pawn pawn)
        {
            if (pawn == null) return false;
            if (PawnInside != null) return false;

            // Despawn -> contained (vanilla-style container occupant)
            if (pawn.Spawned)
                pawn.DeSpawn();

            bool ok = pawnContainer.TryAdd(pawn, canMergeWithExistingStacks: false);
            return ok;
        }

        // Altijd toegestaan
        public void EjectPawn()
        {
            if (pawnContainer == null || pawnContainer.Count == 0) return;

            IntVec3 dropSpot = this.InteractionCell;
            if (!dropSpot.InBounds(this.Map) || !dropSpot.Standable(this.Map))
                dropSpot = this.Position;

            pawnContainer.TryDropAll(dropSpot, this.Map, ThingPlaceMode.Near);
        }

        // --- Soft stasis tick: health pauze, hunger door ---
        protected override void Tick()
        {
            base.Tick();

            Pawn p = PawnInside;
            if (p == null) return;

            // Stasis blijft actief zelfs zonder power (zoals je vroeg),
            // dus we checken CanOperateNow hier NIET.

            // Hunger interval tick
            if (this.IsHashIntervalTick(HungerIntervalTicks))
            {
                TickHungerOnly(p);
            }
        }

        private void TickHungerOnly(Pawn p)
        {
            if (p == null || p.Dead) return;

            // Alleen food need laten lopen (rest/joy pauze)
            Need_Food food = p.needs?.food;
            if (food == null) return;

            // Vanilla cadence: NeedInterval
            food.NeedInterval();

            // Als hunger op is: bouw malnutrition op (health ticks zijn gepauzeerd)
            if (food.CurLevel <= 0.0001f)
            {
                HediffDef malDef = GetMalnutritionDef();
                if (malDef != null)
                {
                    HealthUtility.AdjustSeverity(p, malDef, MalnutritionPerInterval);

                    Hediff mal = p.health?.hediffSet?.GetFirstHediffOfDef(malDef);
                    if (mal != null && mal.Severity >= 1.0f)
                    {
                        // Sterven netjes afhandelen: spawn pawn op map -> Kill -> corpse verschijnt correct
                        SpawnPawnForDeath(p);
                        p.Kill(null);
                    }
                }
            }
        }

        private HediffDef GetMalnutritionDef()
        {
            if (cachedMalnutritionDef != null) return cachedMalnutritionDef;

            // Prefer DefOf (best), maar fallback via name (safe)
            cachedMalnutritionDef = HediffDefOf.Malnutrition
                ?? DefDatabase<HediffDef>.GetNamedSilentFail("Malnutrition");

            return cachedMalnutritionDef;
        }

        private void SpawnPawnForDeath(Pawn p)
        {
            if (p == null) return;

            if (pawnContainer != null && pawnContainer.Contains(p))
                pawnContainer.Remove(p);

            IntVec3 c = this.InteractionCell;
            if (!c.InBounds(Map) || !c.Standable(Map))
                c = this.Position;

            // Spawn pawn back so that Kill() yields a corpse in-map (reliable)
            GenSpawn.Spawn(p, c, Map, WipeMode.Vanish);

        }

        // --- Float menu (op machine) ---
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (var opt in base.GetFloatMenuOptions(selPawn))
                yield return opt;

            // Zelf instappen (power-gated)
            string reason;
            bool canEnter = CanAcceptPawnNow(selPawn, out reason);

            if (!canEnter)
            {
                yield return new FloatMenuOption($"Enter Surgery Machine ({reason})", null);
            }
            else
            {
                yield return new FloatMenuOption("Enter Surgery Machine", () =>
                {
                    var job = JobMaker.MakeJob(SM_DefOf.RHK_EnterSurgeryMachine, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }

            // Load carried item (altijd toegestaan)
            var inv = InventoryComp;
            if (inv != null)
            {
                Thing carried = selPawn.carryTracker?.CarriedThing;
                if (carried != null && !(carried is Pawn))
                {
                    if (!selPawn.CanReserve(this))
                    {
                        yield return new FloatMenuOption($"Load carried item ({carried.Label}) (Cannot reserve machine)", null);
                    }
                    else
                    {
                        yield return new FloatMenuOption($"Load carried item ({carried.Label})", () =>
                        {
                            var job = JobMaker.MakeJob(SM_DefOf.RHK_InsertCarriedThingIntoSurgeryMachine, this);
                            selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        });
                    }
                }
            }
        }

        // --- Gizmos ---
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos())
                yield return g;

            Gizmo selectGizmo = Building.SelectContainedItemGizmo(this, PawnInside);
            if (selectGizmo != null)
                yield return selectGizmo;

            var eject = new Command_Action
            {
                defaultLabel = "Eject pawn",
                defaultDesc = "Eject the pawn from the machine.",
                icon = ContentFinder<Texture2D>.Get("Icons/ExitSurgeryMachine", true),
                action = EjectPawn
            };

            if (PawnInside == null)
                eject.Disable("No pawn inside.");

            yield return eject;
        }

        // --- Inspect string: toon pawn + items ---
        public override string GetInspectString()
        {
            string baseStr = base.GetInspectString();

            Pawn inside = PawnInside;
            string pawnLine = inside != null ? $"Contains pawn: {inside.LabelShortCap}" : "Contains pawn: none";

            int itemStacks = InventoryComp?.Count ?? 0;
            string itemLine = itemStacks > 0 ? $"Stored items: {itemStacks} stacks" : "Stored items: none";

            if (baseStr.NullOrEmpty())
                return pawnLine + "\n" + itemLine;

            return baseStr + "\n" + pawnLine + "\n" + itemLine;
        }

        // --- Draw pawn inside (biosculpter/subcore-style) ---
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);

            Pawn p = PawnInside;
            if (p == null) return;

            Vector3 pawnDrawLoc = drawLoc + new Vector3(0f, 0.02f, 0.15f);

            try
            {
                p.Drawer.renderer.RenderPawnAt(pawnDrawLoc, Rot4.South);
            }
            catch (Exception e)
            {
                Log.Warning("[SurgeryMachine] Failed to draw pawn inside: " + e.Message);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // Bij destroy: drop pawn zodat je geen “verdwenen pawn” krijgt
            if (mode != DestroyMode.Vanish && pawnContainer != null && pawnContainer.Count > 0 && this.Map != null)
            {
                IntVec3 dropSpot = this.InteractionCell;
                if (!dropSpot.InBounds(this.Map) || !dropSpot.Standable(this.Map))
                    dropSpot = this.Position;

                pawnContainer.TryDropAll(dropSpot, this.Map, ThingPlaceMode.Near);
            }

            base.Destroy(mode);
        }
    }
}
