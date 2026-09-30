using RetroSk8.Audio;
using RetroSk8.Data;
using RetroSk8.Input;
using RetroSk8.Level;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Player
{
    /// <summary>Assembles the placeholder skater from code and wires its components. Replace with a prefab later.</summary>
    public static class SkaterFactory
    {
        public static PlayerController Create(Transform spawn, PlayerInputRouter input, ComboManager combo,
            ContentRegistry content, ScoringProfile profile, LevelInfo level)
        {
            var go = new GameObject("Skater");
            go.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(go.transform, false);
            var visual = visualRoot.AddComponent<SkaterVisual>();
            visual.Build();

            var player = go.AddComponent<PlayerController>();
            var tricks = go.AddComponent<TrickController>();
            var grind = go.AddComponent<GrindController>();
            var manual = go.AddComponent<ManualController>();
            var bail = go.AddComponent<BailHandler>();
            var respawn = go.AddComponent<RespawnSafety>();
            var audio = go.AddComponent<SkaterAudio>();
            var gaps = go.AddComponent<GapTracker>();

            var library = content.trickLibrary;
            player.Init(input, combo, profile, library);
            gaps.Init(player, combo);
            visual.ApplyLoadout(RetroSk8.Game.CosmeticsService.CurrentLoadout(content));
            tricks.Init(player, combo, library, visual);
            grind.Init(player, combo, library, visual, bail, profile);
            manual.Init(player, combo, library, visual, bail, profile);
            bail.Init(player, combo, respawn);
            respawn.Init(player, bail, level);
            audio.Init(player, combo, tricks, grind, manual, bail);

            player.Teleport(spawn.position, spawn.forward);
            return player;
        }
    }
}
