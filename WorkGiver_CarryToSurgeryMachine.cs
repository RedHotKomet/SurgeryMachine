using RimWorld;
using Verse;
using Verse.AI;

namespace RedHotKomet.SurgeryMachine
{
    public class WorkGiver_CarryToSurgeryMachine : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn patient = t as Pawn;
            if (patient == null) return false;
            if (patient == pawn) return false;
            if (!patient.Spawned) return false;
            if (patient.Dead) return false;
            if (!patient.Downed) return false;

            // basic reach/reserve
            if (!pawn.CanReserve(patient, 1, -1, null, forced)) return false;
            if (!pawn.CanReach(patient, PathEndMode.OnCell, Danger.Deadly)) return false;

            // Find best machine
            Building_SurgeryMachine machine = FindBestMachineFor(pawn, patient);
            if (machine == null) return false;

            if (!pawn.CanReserve(machine, 1, -1, null, forced)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Pawn patient = (Pawn)t;
            Building_SurgeryMachine machine = FindBestMachineFor(pawn, patient);
            if (machine == null) return null;

            Job job = JobMaker.MakeJob(SM_DefOf.RHK_CarryToSurgeryMachine, patient, machine);
            job.count = 1; // voorkomt “Invalid count -1”
            return job;
        }

        private Building_SurgeryMachine FindBestMachineFor(Pawn hauler, Pawn patient)
        {
            Map map = hauler.Map;
            if (map == null) return null;

            Building_SurgeryMachine best = null;
            float bestDist = 999999f;

            foreach (var b in map.listerBuildings.AllBuildingsColonistOfClass<Building_SurgeryMachine>())
            {
                if (b == null || b.Destroyed) continue;
                if (b.PawnInside != null) continue;
                if (!b.CanOperateNow) continue; // entry/carry power-gated
                if (!hauler.CanReach(b, PathEndMode.InteractionCell, Danger.Deadly)) continue;
                if (!hauler.CanReserve(b)) continue;

                float d = (b.Position - hauler.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = b;
                }
            }

            return best;
        }
    }
}
