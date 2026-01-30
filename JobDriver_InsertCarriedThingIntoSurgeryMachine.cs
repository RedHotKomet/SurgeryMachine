using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    /// <summary>
    /// MVP: als pawn een item draagt, kan hij het in de machine steken (altijd toegestaan, ook zonder power).
    /// </summary>
    public class JobDriver_InsertCarriedThingIntoSurgeryMachine : JobDriver
    {
        private Building_SurgeryMachine Machine => job.targetA.Thing as Building_SurgeryMachine;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => Machine == null || Machine.InventoryComp == null);
            this.FailOn(() => pawn.carryTracker?.CarriedThing == null);
            this.FailOn(() => pawn.carryTracker.CarriedThing is Pawn);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var insert = new Toil();
            insert.initAction = () =>
            {
                var m = Machine;
                var inv = m?.InventoryComp;
                if (m == null || inv == null) return;

                Thing carried = pawn.carryTracker.CarriedThing;
                if (carried == null || carried is Pawn) return;

                // Drop carried item direct op positie, dan voeg toe aan inventory
                if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out Thing dropped))
                {
                    if (!inv.CanAcceptNow(dropped) || !inv.TryAddInternal(dropped))
                    {
                        // Fail-safe: item blijft gewoon op de grond liggen
                        Messages.Message("Machine inventory is full.", MessageTypeDefOf.RejectInput);
                    }
                }
                else
                {
                    Messages.Message("Failed to drop carried item.", MessageTypeDefOf.RejectInput);
                }
            };
            insert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insert;
        }
    }
}
