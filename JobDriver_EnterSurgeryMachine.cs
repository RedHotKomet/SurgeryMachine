using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    public class JobDriver_EnterSurgeryMachine : JobDriver
    {
        private Building_SurgeryMachine Machine => job.targetA.Thing as Building_SurgeryMachine;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var enter = new Toil();
            enter.initAction = () =>
            {
                var m = Machine;
                if (m == null) return;

                string reason;
                if (!m.CanAcceptPawnNow(pawn, out reason))
                {
                    Messages.Message("Cannot enter Surgery Machine: " + reason, MessageTypeDefOf.RejectInput);
                    return;
                }

                if (!m.TryAcceptPawn(pawn))
                    Messages.Message("Failed to enter Surgery Machine.", MessageTypeDefOf.RejectInput);
            };
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
        }
    }
}
