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
        public const string RetroCity = "retro_city";
        public const string SunsetBowls = "sunset_bowls";

        public const string FountainGap = "fountain_gap";
        public const string ContainerGap = "container_gap";
        public const string ConveyorGap = "conveyor_gap";
        public const string DockGap = "dock_gap";
        public const string SpineTransfer = "spine_transfer";
        public const string RooftopGap = "rooftop_gap";
        public const string ChannelGap = "channel_gap";
        public const string CivicEight = "civic_eight";
        public const string DoubleSet = "double_set";
        public const string CanalJump = "canal_jump";
        public const string PlanterGap = "planter_gap";
        public const string DockGapCity = "dock_gap_city";
        public const string DeepEndAir = "deep_end_air";
        public const string VolcanoHop = "volcano_hop";
        public const string FunboxHop = "funbox_hop";

        /// <summary>Every park the game can build, in menu order.</summary>
        public static readonly string[] All = { HarborPlaza, NeonWarehouse, RooftopRun, SunsetBowls, RetroCity };

        public static bool HasBuilder(string locationId) => System.Array.IndexOf(All, locationId) >= 0;

        public static string SceneFor(string locationId)
        {
            switch (locationId)
            {
                case NeonWarehouse: return "SkateScene_NeonWarehouse";
                case RooftopRun: return "SkateScene_RooftopRun";
                case RetroCity: return "SkateScene_RetroCity";
                case SunsetBowls: return "SkateScene_SunsetBowls";
                default: return "SkateScene_HarborPlaza";
            }
        }

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
                case RetroCity:
                    list.Add(new DailyLineGenerator.Gap(CivicEight, "Civic 8"));
                    list.Add(new DailyLineGenerator.Gap(DoubleSet, "Downtown Double Set"));
                    list.Add(new DailyLineGenerator.Gap(CanalJump, "Canal Jump"));
                    list.Add(new DailyLineGenerator.Gap(PlanterGap, "Planter Gap"));
                    list.Add(new DailyLineGenerator.Gap(DockGapCity, "Dock Gap"));
                    break;
                case SunsetBowls:
                    list.Add(new DailyLineGenerator.Gap(DeepEndAir, "Deep End Air"));
                    list.Add(new DailyLineGenerator.Gap(VolcanoHop, "Volcano Hop"));
                    list.Add(new DailyLineGenerator.Gap(FunboxHop, "Funbox Hop"));
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
                case RetroCity: b = go.AddComponent<RetroCityBuilder>(); break;
                case SunsetBowls: b = go.AddComponent<SunsetBowlsBuilder>(); break;
                default: b = go.AddComponent<HarborPlazaBuilder>(); break;
            }
            b.buildOnAwake = false;
            return b;
        }
    }
}
