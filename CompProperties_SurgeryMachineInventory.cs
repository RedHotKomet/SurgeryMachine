using Verse;

namespace RedHotKomet.SurgeryMachine
{
    public class CompProperties_SurgeryMachineInventory : CompProperties
    {
        public int maxItemStacks = 30;

        public CompProperties_SurgeryMachineInventory()
        {
            compClass = typeof(Comp_SurgeryMachineInventory);
        }

        public override void ResolveReferences(ThingDef parentDef)
        {
            Log.Message($"[SurgeryMachine] ResolveReferences: compClass now {compClass?.FullName}");

            base.ResolveReferences(parentDef);

            // Bulletproof: als om welke reden dan ook compClass niet goed staat, fix het hier.
            if (compClass == null || compClass == typeof(ThingComp))
            {
                compClass = typeof(Comp_SurgeryMachineInventory);
            }
        }
    }
}
