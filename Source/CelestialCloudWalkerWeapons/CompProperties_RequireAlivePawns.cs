using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_RequireAlivePawns : CompProperties_AbilityEffect
    {
        public List<PawnKindDef> requiredPawnKinds = new List<PawnKindDef>();
        public string failMessage = "Required clones are not all present.";

        public CompProperties_RequireAlivePawns()
        {
            compClass = typeof(CompAbilityEffect_RequireAlivePawns);
        }
    }

    public class CompAbilityEffect_RequireAlivePawns : CompAbilityEffect
    {
        public new CompProperties_RequireAlivePawns Props =>
            (CompProperties_RequireAlivePawns)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;
            Pawn caster = parent.pawn;
            if (caster?.Map == null) return false;

            List<Pawn> aliveFactionPawns = caster.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction == caster.Faction && !p.Dead && !p.Destroyed)
                .ToList();

            foreach (PawnKindDef required in Props.requiredPawnKinds)
            {
                bool found = aliveFactionPawns.Any(p => p.kindDef == required);
                if (!found)
                {
                    if (throwMessages)
                        Messages.Message(Props.failMessage, MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest) { }
    }

    public class CompProperties_DespawnPawnKinds : CompProperties_AbilityEffect
    {
        public List<PawnKindDef> pawnKindsToDespawn = new List<PawnKindDef>();
        public bool silentDestroy = true;
        public HediffDef protectCasterHediff;
        public HediffDef casterHediffToApply;
        public List<HediffDef> casterHediffsToSwapOut = new List<HediffDef>();

        public CompProperties_DespawnPawnKinds()
        {
            compClass = typeof(CompAbilityEffect_DespawnPawnKinds);
        }
    }

    public class CompAbilityEffect_DespawnPawnKinds : CompAbilityEffect
    {
        public new CompProperties_DespawnPawnKinds Props =>
            (CompProperties_DespawnPawnKinds)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) => true;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            if (Props.casterHediffToApply != null && caster.health?.hediffSet != null)
            {
                foreach (HediffDef swapOut in Props.casterHediffsToSwapOut)
                {
                    if (swapOut == null) continue;
                    Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(swapOut);
                    if (existing != null) caster.health.RemoveHediff(existing);
                }
                if (!caster.health.hediffSet.HasHediff(Props.casterHediffToApply))
                    caster.health.AddHediff(HediffMaker.MakeHediff(Props.casterHediffToApply, caster));
            }

            List<Pawn> toRemove = caster.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != caster
                    && p.Faction == caster.Faction
                    && !p.Dead
                    && !p.Destroyed
                    && Props.pawnKindsToDespawn.Contains(p.kindDef))
                .ToList();

            foreach (Pawn p in toRemove)
            {
                if (Props.silentDestroy)
                {
                    if (p.Spawned) p.DeSpawn(DestroyMode.Vanish);
                    p.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    p.Kill(null);
                }
            }

            if (Props.protectCasterHediff != null && caster.health?.hediffSet != null)
            {
                if (!caster.health.hediffSet.HasHediff(Props.protectCasterHediff))
                    caster.health.AddHediff(HediffMaker.MakeHediff(Props.protectCasterHediff, caster));
            }
        }
    }

    public class HediffCompProperties_NotifyCasterOnRemoved : HediffCompProperties
    {
        public List<string> watchedPawnKindDefNames = new List<string>();
        public HediffDef casterHediffToRemove;
        public HediffDef skipRemovalIfCasterHas;
        public int minTicksBeforeFire = 60;

        public HediffCompProperties_NotifyCasterOnRemoved()
        {
            compClass = typeof(HediffComp_NotifyCasterOnRemoved);
        }
    }

    public class HediffComp_NotifyCasterOnRemoved : HediffComp
    {
        public HediffCompProperties_NotifyCasterOnRemoved Props =>
            (HediffCompProperties_NotifyCasterOnRemoved)props;

        private Map cachedMap;
        private int addedAtTick = -1;

        public override void CompPostMake()
        {
            base.CompPostMake();
            cachedMap = Pawn?.Map;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Pawn?.Map != null)
                cachedMap = Pawn.Map;
            addedAtTick = Find.TickManager?.TicksGame ?? 0;
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            Pawn clone = Pawn;
            Map map = cachedMap ?? clone?.Map;
            if (map == null) return;
            if (Props.casterHediffToRemove == null) return;

            if (Props.minTicksBeforeFire > 0 && addedAtTick >= 0)
            {
                int currentTick = Find.TickManager?.TicksGame ?? 0;
                int ticksAlive = currentTick - addedAtTick;
                if (ticksAlive < Props.minTicksBeforeFire)
                {
                    return;
                }
            }

            HashSet<string> watched = new HashSet<string>(Props.watchedPawnKindDefNames);

            int remainingClones = map.mapPawns.AllPawnsSpawned
                .Count(p => p != clone
                    && p.Faction == clone.Faction
                    && !p.Dead
                    && !p.Destroyed
                    && p.kindDef != null
                    && watched.Contains(p.kindDef.defName));

            if (remainingClones > 0) return;

            Pawn caster = map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p != clone
                    && p.Faction == clone.Faction
                    && !p.Dead
                    && p.health?.hediffSet != null
                    && p.health.hediffSet.HasHediff(Props.casterHediffToRemove));

            if (caster == null) return;

            if (Props.skipRemovalIfCasterHas != null
                && caster.health.hediffSet.HasHediff(Props.skipRemovalIfCasterHas))
                return;

            Hediff toRemove = caster.health.hediffSet
                .GetFirstHediffOfDef(Props.casterHediffToRemove);

            if (toRemove != null)
                caster.health.RemoveHediff(toRemove);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref cachedMap, "cachedMap");
            Scribe_Values.Look(ref addedAtTick, "addedAtTick", -1);
        }
    }


    public class CompProperties_ApplyCasterHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffToApply;
        public List<HediffDef> hediffsToRemove = new List<HediffDef>();

        public CompProperties_ApplyCasterHediff()
        {
            compClass = typeof(CompAbilityEffect_ApplyCasterHediff);
        }
    }

    public class CompAbilityEffect_ApplyCasterHediff : CompAbilityEffect
    {
        public new CompProperties_ApplyCasterHediff Props =>
            (CompProperties_ApplyCasterHediff)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) => true;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.health?.hediffSet == null) return;

            foreach (HediffDef remove in Props.hediffsToRemove)
            {
                if (remove == null) continue;
                Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(remove);
                if (existing != null)
                    caster.health.RemoveHediff(existing);
            }

            if (Props.hediffToApply != null
                && !caster.health.hediffSet.HasHediff(Props.hediffToApply))
            {
                caster.health.AddHediff(
                    HediffMaker.MakeHediff(Props.hediffToApply, caster));
            }
        }
    }

}