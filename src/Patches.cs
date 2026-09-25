using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace SlowBurnRomance
{
    /// <summary>
    /// The pair an interaction is being tested for. The game tests an interaction's "us" and
    /// "them" triggers inside Interaction.TriggeredInternal, so that's where both are known.
    /// A stack, since testing one interaction can test others.
    /// </summary>
    internal static class Pair
    {
        [ThreadStatic] private static Stack<(CondOwner us, CondOwner them)> _stack;

        internal static CondOwner Us => _stack != null && _stack.Count > 0 ? _stack.Peek().us : null;
        internal static CondOwner Them => _stack != null && _stack.Count > 0 ? _stack.Peek().them : null;

        internal static void Push(CondOwner us, CondOwner them) => (_stack ??= new Stack<(CondOwner, CondOwner)>()).Push((us, them));
        internal static void Pop() { if (_stack != null && _stack.Count > 0) _stack.Pop(); }
    }

    [HarmonyPatch(typeof(Interaction), "TriggeredInternal")]
    internal static class InteractionPairPatch
    {
        private static void Prefix(CondOwner objUs, CondOwner objThem) => Pair.Push(objUs, objThem);

        private static Exception Finalizer(Exception __exception)
        {
            Pair.Pop();
            return __exception;
        }
    }

    /// <summary>
    /// Every romantic interaction is gated by the attraction flags (IsAttractedMen, -Women, -NB),
    /// tested by triggers that either require the flag or forbid it. When an open person is tested
    /// toward someone they've grown close to, a trigger that requires the flag matching that
    /// person's gender is evaluated as a copy without that one requirement, and a trigger that
    /// forbids it fails, exactly as if the flag were there. Nothing is written to anyone's
    /// conditions, and only this pair is affected.
    /// </summary>
    [HarmonyPatch(typeof(CondTrigger), nameof(CondTrigger.Triggered))]
    internal static class AttractionTriggerPatch
    {
        private static readonly string[] Flags = { "IsAttractedMen", "IsAttractedWomen", "IsAttractedNB" };

        private sealed class Info
        {
            public HashSet<string> Reqs;
            public HashSet<string> Forbids;
        }

        private sealed class ByReference : IEqualityComparer<CondTrigger>
        {
            public bool Equals(CondTrigger a, CondTrigger b) => ReferenceEquals(a, b);
            public int GetHashCode(CondTrigger t) => RuntimeHelpers.GetHashCode(t);
        }

        // The game hands out clones of each trigger, so what a trigger tests is cached by name.
        private static readonly Dictionary<string, Info> InfoByName = new Dictionary<string, Info>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CondTrigger> Relaxed = new Dictionary<string, CondTrigger>(StringComparer.Ordinal);
        private static readonly HashSet<CondTrigger> Ours = new HashSet<CondTrigger>(new ByReference());

        private static bool Prefix(CondTrigger __instance, CondOwner objOwner, string strIAStatsName, bool logOutcome, ref bool __result)
        {
            CondOwner us = Pair.Us;
            if (us == null || objOwner == null) return true;
            CondOwner other = objOwner == us ? Pair.Them : objOwner == Pair.Them ? us : null;
            if (other == null || other == objOwner || Ours.Contains(__instance)) return true;

            Info info = InfoOf(__instance);
            if (info == null) return true;
            string flag = Rules.FlagFor(other);
            if (flag == null) return true;
            bool required = info.Reqs.Contains(flag);
            bool forbidden = info.Forbids.Contains(flag);
            if (!required && !forbidden) return true;
            if (objOwner.HasCond(flag) || !Rules.Opens(objOwner, other)) return true;

            // A forbidden condition that's present fails the trigger on every path through it.
            if (forbidden)
            {
                __result = __instance.bNOT;
                return false;
            }
            __result = RelaxedCopy(__instance, flag).Triggered(objOwner, strIAStatsName, logOutcome);
            return false;
        }

        private static Info InfoOf(CondTrigger t)
        {
            string name = t.strName;
            if (name == null) return null;
            if (InfoByName.TryGetValue(name, out Info info)) return info;
            string[] reqs = t.aReqs ?? new string[0];
            string[] forbids = t.aForbids ?? new string[0];
            if (Flags.Any(f => reqs.Contains(f) || forbids.Contains(f)))
                info = new Info
                {
                    Reqs = new HashSet<string>(reqs.Where(Flags.Contains)),
                    Forbids = new HashSet<string>(forbids.Where(Flags.Contains)),
                };
            InfoByName[name] = info;
            return info;
        }

        private static CondTrigger RelaxedCopy(CondTrigger t, string flag)
        {
            string key = t.strName + "|" + flag;
            if (Relaxed.TryGetValue(key, out CondTrigger copy)) return copy;
            copy = t.Clone();
            copy.aReqs = (t.aReqs ?? new string[0]).Where(r => r != flag).ToArray();
            Ours.Add(copy);
            Relaxed[key] = copy;
            return copy;
        }
    }

    /// <summary>
    /// The one hardcoded attraction check: after an interaction, Relationship.StoreIAConds makes
    /// someone a crush (RELLover) once intimacy reaches −50, but only toward a gender they're
    /// attracted to. This applies the same step for an open person toward someone they've
    /// grown close to.
    /// </summary>
    [HarmonyPatch(typeof(Relationship), nameof(Relationship.StoreIAConds))]
    internal static class CrushPatch
    {
        private const double CrushIntimacy = -50.0;

        private static void Postfix(Relationship __instance, CondOwner coUs, Dictionary<string, double> dict, CondOwner coThem)
        {
            if (dict == null || coUs == null || coThem == null || !coUs.bAlive || coUs.HasCond("Unconscious")) return;
            List<string> rels = __instance.aRelationships;
            if (rels == null || rels.Contains("RELLover")) return;
            if (!__instance.Conds.TryGetValue("StatIntimacy", out double intimacy) || intimacy > CrushIntimacy) return;
            string flag = Rules.FlagFor(coThem);
            if (flag == null || coUs.HasCond(flag) || !Rules.Opens(coUs, coThem)) return;

            __instance.RemoveRelationship(coUs, "RELAcquaintance");
            __instance.RemoveRelationship(coUs, "RELStranger");
            __instance.RemoveRelationship(coUs, "RELLoverEx");
            __instance.AddRelationship(coUs, "RELLover");
        }
    }
}
