using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    public class JobDriver_CarryToSurgeryMachine : JobDriver
    {
        private const TargetIndex TakeeInd = TargetIndex.A;
        private const TargetIndex MachineInd = TargetIndex.B;

        private Pawn Takee => (Pawn)job.GetTarget(TakeeInd).Thing;
        private Building_SurgeryMachine Machine => (Building_SurgeryMachine)job.GetTarget(MachineInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(Machine, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TakeeInd);
            this.FailOnDestroyedOrNull(MachineInd);

            // machine moet operationeel om een pawn erin te steken
            this.FailOn(() => Machine == null || !Machine.CanOperateNow);

            // machine mag niet bezet zijn
            this.FailOn(() => Machine.PawnInside != null);

            // Ga naar takee (vanilla: OnCell)
            Toil goToTakee = Toils_Goto.GotoThing(TakeeInd, PathEndMode.OnCell)
                .FailOnSomeonePhysicallyInteracting(TakeeInd);

            // Start carry (vanilla signature)
            Toil startCarry = Toils_Haul.StartCarryThing(TakeeInd, false, false, false, true, false);

            // Ga naar machine interaction cell
            Toil goToMachine = Toils_Goto.GotoThing(MachineInd, PathEndMode.InteractionCell);

            // Als we takee al dragen, skip naar machine (vanilla JumpIf)
            yield return Toils_Jump.JumpIf(goToMachine, () => pawn.IsCarryingPawn(Takee));

            yield return goToTakee;
            yield return startCarry;
            yield return goToMachine;

            // Kleine delay zoals vanilla (optioneel)
            Toil wait = Toils_General.Wait(60, MachineInd);
            wait.FailOnCannotTouch(MachineInd, PathEndMode.InteractionCell);
            wait.WithProgressBarToilDelay(MachineInd);
            yield return wait;

            // Insert (BELANGRIJK: eerst uit carryTracker halen)
            Toil insert = ToilMaker.MakeToil();
            insert.initAction = () =>
            {
                var m = Machine;
                var takee = Takee;
                if (m == null || takee == null) return;

                if (!pawn.IsCarryingPawn(takee))
                {
                    Messages.Message("Could not carry pawn to Surgery Machine.", MessageTypeDefOf.RejectInput);
                    return;
                }

                // Haal takee uit carry container, dan accept
                pawn.carryTracker.innerContainer.Remove(takee);

                if (!m.TryAcceptPawn(takee))
                {
                    // Fail-safe: probeer terug op de grond te plaatsen
                    GenPlace.TryPlaceThing(takee, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                    Messages.Message("Failed to insert pawn into Surgery Machine.", MessageTypeDefOf.RejectInput);
                }
            };
            insert.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insert;
        }
    }
}
