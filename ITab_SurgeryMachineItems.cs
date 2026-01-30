using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RedHotKomet.SurgeryMachine
{
    public class ITab_SurgeryMachineItems : ITab
    {
        private Vector2 scrollPos = Vector2.zero;
        private const float RowHeight = 28f;
        private const float ButtonW = 64f;

        public ITab_SurgeryMachineItems()
        {
            size = new Vector2(520f, 420f);
            labelKey = "RHK_SurgeryMachine_ItemsTab";
        }

        private Building_SurgeryMachine Machine => SelThing as Building_SurgeryMachine;
        private Comp_SurgeryMachineInventory Inv => Machine?.InventoryComp;

        protected override void FillTab()
        {
            var m = Machine;
            var inv = Inv;

            Rect outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);

            if (m == null || inv == null)
            {
                Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 30f), "No inventory.");
                return;
            }

            IntVec3 dropSpot = m.InteractionCell;
            if (!dropSpot.InBounds(m.Map) || !dropSpot.Standable(m.Map))
                dropSpot = m.Position;

            // Header
            Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, 30f), $"Stored items ({inv.Count})");

            // (optioneel) Drop all knop (kan je later wegdoen)
            Rect dropAllRect = new Rect(outRect.xMax - 110f, outRect.y, 110f, 30f);
            if (Widgets.ButtonText(dropAllRect, "Drop all"))
            {
                inv.DropAll(dropSpot, m.Map);
            }

            var items = inv.Contents?.Where(t => t != null).ToList();
            if (items == null || items.Count == 0)
            {
                Widgets.Label(new Rect(outRect.x, outRect.y + 40f, outRect.width, 30f), "No items stored.");
                return;
            }

            float topY = outRect.y + 40f;
            Rect scrollOut = new Rect(outRect.x, topY, outRect.width, outRect.height - 40f);
            float viewH = items.Count * RowHeight + 10f;
            Rect viewRect = new Rect(0f, 0f, scrollOut.width - 16f, viewH);

            Widgets.BeginScrollView(scrollOut, ref scrollPos, viewRect);

            float y = 0f;
            foreach (var t in items)
            {
                // Label
                Rect labelRect = new Rect(0f, y, viewRect.width - (ButtonW * 3f) - 12f, RowHeight);
                Widgets.Label(labelRect, t.LabelCap);

                // Buttons
                Rect b1 = new Rect(viewRect.width - (ButtonW * 3f), y + 2f, ButtonW, RowHeight - 4f);
                Rect b10 = new Rect(viewRect.width - (ButtonW * 2f), y + 2f, ButtonW, RowHeight - 4f);
                Rect ball = new Rect(viewRect.width - (ButtonW * 1f), y + 2f, ButtonW, RowHeight - 4f);

                if (Widgets.ButtonText(b1, "Drop 1"))
                    inv.TryDropThing(t, 1, dropSpot, m.Map);

                if (Widgets.ButtonText(b10, "Drop 10"))
                    inv.TryDropThing(t, 10, dropSpot, m.Map);

                if (Widgets.ButtonText(ball, "Drop"))
                    inv.TryDropThing(t, t.stackCount, dropSpot, m.Map);

                y += RowHeight;
            }

            Widgets.EndScrollView();
        }
    }
}
