using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SlowBurnRomance
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.sylvia.slowburnromance";
        public const string Name = "Slow Burn Romance";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            Rules.Enabled = Config.Bind("General", "Enabled", true,
                "Turn the mod off without uninstalling it. Off, attraction works exactly as in the base game.");
            Rules.OpenChance = Config.Bind("Openness", "OpenChance", 0.35f,
                new ConfigDescription(
                    "Share of characters who can come to feel attraction outside their usual orientation, toward someone they've "
                    + "grown close to. Each character's openness is fixed, so raising this only adds open characters and lowering "
                    + "it only removes them. Characters attracted to no one are never affected.",
                    new AcceptableValueRange<float>(0f, 1f)));
            Rules.FamiliarityNeeded = Config.Bind("Closeness", "FamiliarityNeeded", 120f,
                new ConfigDescription(
                    "How familiar an open character must be with someone before attraction can grow, on the game's own scale: "
                    + "strangers become acquaintances at 50, and friend or enemy is decided from 200.",
                    new AcceptableValueRange<float>(0f, 350f)));
            Rules.KindnessNeeded = Config.Bind("Closeness", "KindnessNeeded", 0.6f,
                new ConfigDescription(
                    "Share of the relationship that must have been kind, as the game tallies it (friendship needs over 0.55).",
                    new AcceptableValueRange<float>(0f, 1f)));
            Rules.IncludeNpcPairs = Config.Bind("Scope", "IncludeNpcPairs", false,
                "Off: only relationships involving the player or their crew. On: NPCs can grow attracted to each other too.");

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
