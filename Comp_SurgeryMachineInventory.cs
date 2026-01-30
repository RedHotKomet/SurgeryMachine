using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RedHotKomet.SurgeryMachine
{
    /// <summary>
    /// Item-inventory comp. Owner is de parent Building_SurgeryMachine (IThingHolder).
    /// </summary>
    public class Comp_SurgeryMachineInventory : ThingComp
    {
        private ThingOwner<Thing> itemContainer;

        private CompProperties_SurgeryMachineInventory Props => (CompProperties_SurgeryMachineInventory)props;

        private IThingHolder HolderOwner => parent as IThingHolder;

        private void EnsureContainer()
        {
            if (itemContainer == null)
                itemContainer = new ThingOwner<Thing>(HolderOwner, oneStackOnly: false);
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            EnsureContainer();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            EnsureContainer();
            // Gebruik unieke labelnaam om verwarring te vermijden met pawnContainer.
            Scribe_Deep.Look(ref itemContainer, "itemContainer", HolderOwner);
        }

        public IEnumerable<Thing> Contents
        {
            get
            {
                EnsureContainer();
                return itemContainer;
            }
        }

        public int Count => itemContainer?.Count ?? 0;

        public bool CanAcceptNow(Thing t)
        {
            if (t == null) return false;
            EnsureContainer();

            // simpele stack-limit
            if (itemContainer.Count >= Props.maxItemStacks)
                return false;

            return true;
        }

        /// <summary>
        /// Caller moet ervoor zorgen dat Thing despawned / uit carry gehaald is indien nodig.
        /// </summary>
        public bool TryAddInternal(Thing t)
        {
            if (!CanAcceptNow(t)) return false;
            EnsureContainer();
            return itemContainer.TryAdd(t, canMergeWithExistingStacks: true);
        }

        public bool TryDropThing(Thing t, int count, IntVec3 dropSpot, Map map)
        {
            if (t == null || map == null) return false;
            EnsureContainer();

            // Split off gewenst aantal
            int toDrop = count;
            if (toDrop <= 0) return false;

            if (t.stackCount <= toDrop)
            {
                // drop volledige stack
                if (itemContainer.TryDrop(t, dropSpot, map, ThingPlaceMode.Near, out _))
                    return true;

                return false;
            }
            else
            {
                Thing split = t.SplitOff(toDrop);
                if (itemContainer.TryDrop(split, dropSpot, map, ThingPlaceMode.Near, out _))
                    return true;

                // Fail-safe: als drop faalt, probeer terug te mergen
                t.TryAbsorbStack(split, true);
                return false;
            }
        }

        public void DropAll(IntVec3 dropSpot, Map map)
        {
            if (map == null) return;
            EnsureContainer();
            itemContainer.TryDropAll(dropSpot, map, ThingPlaceMode.Near);
        }

        public override string CompInspectStringExtra()
        {
            // mini info in inspect pane
            EnsureContainer();
            int stacks = itemContainer.Count;
            return stacks > 0 ? $"Stored items: {stacks}" : "Stored items: none";
        }
    }
}
