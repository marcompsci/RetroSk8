using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// One place that knows which builder makes which park and which named gaps each park has.
    /// Menus use the gap list to preview the Daily Line without building the level.
    /// </summary>
    public static class ParkCatalog
    {
        public const string HarborPlaza = "harbor_plaza";
        public const string NeonWarehouse = "neon_warehouse";
        public const string RooftopRun = "rooftop_run";

        public const string FountainGap = "fountain_gap";
        public const string ContainerGap = "container_gap";
        public const string ConveyorGap = "conveyor_gap";
        public const string DockGap = "dock_gap";
        public const string SpineTransfer = "spine_transfer";
        public const string RooftopGap = "rooftop_gap";
        public const string ChannelGap = "channel_gap";

        public static List<DailyLineGenerator.Gap> GapsFor(string locationId)
        {
            var list = new List<DailyLineGenerator.Gap>();
            switch (locationId)
            {
                case NeonWarehouse:
                    list.Add(new DailyLineGenerator.Gap(ConveyorGap, "Conveyor Gap"));
                    list.Add(new DailyLineGenerator.Gap(DockGap, "Dock Gap"));
                    list.Add(new DailyLineGenerator.Gap(SpineTransfer, "Spine Transfer"));
                    break;
                case RooftopRun:
                    list.Add(new DailyLineGenerator.Gap(RooftopGap, "Rooftop Gap"));
                    list.Add(new DailyLineGenerator.Gap(ChannelGap, "Channel Gap"));
                    break;
                default:
                    list.Add(new DailyLineGenerator.Gap(FountainGap, "Fountain Gap"));
                    list.Add(new DailyLineGenerator.Gap(ContainerGap, "Container Gap"));
                    break;
            }
            return list;
        }

        /// <summary>Adds the right builder for a park to <paramref name="go"/> (used when a scene has none).</summary>
        public static ParkBuilder AddBuilder(GameObject go, string locationId)
        {
            ParkBuilder b;
            switch (locationId)
            {
                case NeonWarehouse: b = go.AddComponent<NeonWarehouseBuilder>(); break;
                case RooftopRun: b = go.AddComponent<RooftopRunBuilder>(); break;
                default: b = go.AddComponent<HarborPlazaBuilder>(); break;
            }
            b.buildOnAwake = false;
            return b;
        }
    }
}
