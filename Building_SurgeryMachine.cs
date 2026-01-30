using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    public class Building_SurgeryMachine : Building, IThingHolder
    {
        private bool addedStasisHediffToPawn;

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
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Als we denken dat we stasis added hebben maar er is geen pawn meer: reset flag
                if (addedStasisHediffToPawn && PawnInside == null)
                {
                    addedStasisHediffToPawn = false;
                }
            }

            Scribe_Values.Look(ref addedStasisHediffToPawn, "addedStasisHediffToPawn", false);

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

            if (pawn.Spawned)
                pawn.DeSpawn();

            // Stasis toepassen zodat hediffs “pauzeren” zoals cryptosleep/biosculpter feel.
            ApplyStasisIfNeeded(pawn);

            bool ok = pawnContainer.TryAdd(pawn, canMergeWithExistingStacks: false);

            if (!ok)
            {
                // Fail-safe: haal stasis weg als we hem niet echt konden opslaan
                RemoveStasisIfWeAddedIt(pawn);
            }

            return ok;
        }


        // Altijd toegestaan
        public void EjectPawn()
        {
            if (pawnContainer == null || pawnContainer.Count == 0) return;

            Pawn p = PawnInside;
            if (p != null)
            {
                RemoveStasisIfWeAddedIt(p);
            }

            IntVec3 dropSpot = this.InteractionCell;
            if (!dropSpot.InBounds(this.Map) || !dropSpot.Standable(this.Map))
                dropSpot = this.Position;

            pawnContainer.TryDropAll(dropSpot, this.Map, ThingPlaceMode.Near);
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

            // Vanilla: selecteer de contained pawn met 1 gizmo
            Gizmo selectGizmo = Building.SelectContainedItemGizmo(this, PawnInside);
            if (selectGizmo != null)
                yield return selectGizmo;

            // Eject pawn (altijd toegestaan)
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
            // Als er een pawn in zit en we gaan contents droppen: eerst stasis verwijderen
            if (mode != DestroyMode.Vanish && PawnInside != null)
            {
                RemoveStasisIfWeAddedIt(PawnInside);
            }

            base.Destroy(mode);
        }


        private HediffDef GetStasisHediffDef()
        {
            // Vanilla def name (veilig lookup, geen hard crash als naam ooit anders is)
            return DefDatabase<HediffDef>.GetNamedSilentFail("Cryptosleep");
        }

        private void ApplyStasisIfNeeded(Pawn p)
        {
            if (p == null) return;

            var def = GetStasisHediffDef();
            if (def == null) return;

            // Als pawn al in stasis/cryptosleep zat, gaan we het later ook niet verwijderen.
            if (p.health?.hediffSet?.HasHediff(def) == true)
            {
                addedStasisHediffToPawn = false;
                return;
            }

            p.health?.AddHediff(def);
            addedStasisHediffToPawn = true;
        }

        private void RemoveStasisIfWeAddedIt(Pawn p)
        {
            if (p == null) return;
            if (!addedStasisHediffToPawn) return;

            var def = GetStasisHediffDef();
            if (def == null) return;

            Hediff h = p.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (h != null)
            {
                p.health.RemoveHediff(h);
            }

            addedStasisHediffToPawn = false;
        }

    }
}
