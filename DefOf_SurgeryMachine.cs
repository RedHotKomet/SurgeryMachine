using RimWorld;
using Verse;

namespace RedHotKomet.SurgeryMachine
{
    [DefOf]
    public static class SM_DefOf
    {
        public static JobDef RHK_EnterSurgeryMachine;
        public static JobDef RHK_CarryToSurgeryMachine;
        public static JobDef RHK_InsertCarriedThingIntoSurgeryMachine;

        static SM_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SM_DefOf));
        }
    }
}
