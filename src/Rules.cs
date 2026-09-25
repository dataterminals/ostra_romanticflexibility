using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace RomanticFlexibility
{
    /// <summary>
    /// When one person may feel attraction toward another outside their usual orientation.
    ///
    /// All of these must hold:
    /// - They're an NPC (the player's orientation stays as chosen) and not attracted to no one.
    /// - Their fixed openness roll falls under OpenChance.
    /// - The pair is in scope (involves the player's crew, unless IncludeNpcPairs).
    /// - Their own relationship with the other person is close and warm enough, measured on the
    ///   game's scale: familiarity and the kind share, computed the way Relationship.StoreIACond
    ///   computes them.
    ///
    /// It's one-way and per pair: an open NPC who grows close to the player may come to feel
    /// attraction toward them, and toward no one else because of it.
    /// </summary>
    public static class Rules
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> OpenChance;
        internal static ConfigEntry<float> FamiliarityNeeded;
        internal static ConfigEntry<float> KindnessNeeded;
        internal static ConfigEntry<bool> IncludeNpcPairs;

        private const double PerStatCap = 25.0;
        private const string Salt = "RomanticFlexibility:";

        /// <summary>The attraction flag that would cover this person, by the game's gender conditions.</summary>
        public static string FlagFor(CondOwner them)
        {
            if (them == null) return null;
            if (them.HasCond("IsFemale")) return "IsAttractedWomen";
            if (them.HasCond("IsMale")) return "IsAttractedMen";
            if (them.HasCond("IsNB")) return "IsAttractedNB";
            return null;
        }

        /// <summary>Whether <paramref name="us"/> may feel attraction toward <paramref name="them"/> they otherwise wouldn't.</summary>
        public static bool Opens(CondOwner us, CondOwner them) => Explain(us, them) == null;

        /// <summary>
        /// Null when <paramref name="us"/> opens up to <paramref name="them"/>; otherwise the first
        /// reason they don't. Public so tools such as OstraScope can show it.
        /// </summary>
        public static string Explain(CondOwner us, CondOwner them)
        {
            if (Enabled == null || !Enabled.Value) return "the mod is off";
            if (us == null || them == null || us == them) return "no pair";
            if (!us.HasCond("IsHuman") || !them.HasCond("IsHuman")) return "not both human";
            if (us.HasCond("IsPlayer")) return "the player's orientation is their own";
            if (us.HasCond("IsAttractedNone")) return "attracted to no one";
            if (!IncludeNpcPairs.Value && !us.HasCond("IsPlayerCrew") && !them.HasCond("IsPlayerCrew")) return "neither is the player's crew";
            if (Openness(us) >= OpenChance.Value) return "not open to it";

            Relationship r = us.socUs?.GetRelationship(them.strName);
            if (r == null) return "they've never met";
            (double fam, double kind) = Closeness(r);
            if (fam < FamiliarityNeeded.Value) return $"familiarity {fam:0.#} of {FamiliarityNeeded.Value:0.#}";
            if (kind < KindnessNeeded.Value) return $"kindness {kind:P0} of {KindnessNeeded.Value:P0}";
            return null;
        }

        /// <summary>
        /// A person's fixed openness in [0, 1): a hash of their name, so it survives saves, loads
        /// and config changes without storing anything.
        /// </summary>
        public static double Openness(CondOwner co)
        {
            string key = Salt + (co?.strName ?? "");
            uint h = 2166136261;
            foreach (char c in key)
            {
                h ^= c;
                h *= 16777619;
            }
            // One more mixing round so similar names don't land on similar values.
            h ^= h >> 15;
            h *= 0x2c1b3c6d;
            h ^= h >> 12;
            return h / 4294967296.0;
        }

        /// <summary>Familiarity and the kind share, the way Relationship.StoreIACond totals them.</summary>
        public static (double familiarity, double kindShare) Closeness(Relationship r)
        {
            double fam = 0, kind = 0;
            Dictionary<string, double> conds = r?.Conds;
            if (conds == null) return (0, 0);
            foreach (double raw in conds.Values)
            {
                double v = Math.Max(-PerStatCap, Math.Min(PerStatCap, raw));
                fam += Math.Abs(v);
                if (v < 0) kind -= v;
            }
            return (fam, fam > 0 ? kind / fam : 0);
        }
    }
}
