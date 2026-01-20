using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace AnimeArsenal
{
    public class CompAbilityEffect_GroundFling : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_GroundFling Props => (CompProperties_AbilityEffect_GroundFling)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (parent?.pawn?.Map == null)
            {
                return;
            }

            Map map = parent.pawn.Map;

            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                return;
            }

            AddGroundEffect(target, map);

            List<Pawn> affectedPawns = GetPawnsInArea(target, map);

            if (!affectedPawns.Any())
            {
                if (Props.showMessages)
                {
                    Messages.Message("No targets in range.", MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            if (Props.maxTargets > 0 && affectedPawns.Count > Props.maxTargets)
            {
                affectedPawns = affectedPawns.InRandomOrder().Take(Props.maxTargets).ToList();
            }

            foreach (Pawn pawn in affectedPawns)
            {
                if (pawn != null && !pawn.Dead)
                {
                    FlingPawn(pawn, map);
                }
            }
        }

        private List<Pawn> GetPawnsInArea(LocalTargetInfo target, Map map)
        {
            List<Pawn> pawns = new List<Pawn>();

            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(target.Cell, Props.effectRadius, true);

            foreach (IntVec3 cell in cells)
            {
                if (cell.InBounds(map))
                {
                    List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                    foreach (Thing thing in things)
                    {
                        if (thing is Pawn pawn && !pawns.Contains(pawn))
                        {
                            if (ShouldAffectPawn(pawn))
                            {
                                pawns.Add(pawn);
                            }
                        }
                    }
                }
            }

            return pawns;
        }

        private bool ShouldAffectPawn(Pawn pawn)
        {
            if (!Props.affectCaster && pawn == parent.pawn)
                return false;

            if (!Props.affectDowned && pawn.Downed)
                return false;

            if (pawn.Dead || pawn.Destroyed)
                return false;

            if (Props.onlyAffectHostiles && parent.pawn != null)
            {
                if (pawn.Faction == parent.pawn.Faction ||
                    (pawn.Faction != null && parent.pawn.Faction != null &&
                     !pawn.Faction.HostileTo(parent.pawn.Faction)))
                {
                    return false;
                }
            }

            return true;
        }

        private void FlingPawn(Pawn pawn, Map map)
        {
            try
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    return;
                }

                if (map == null)
                {
                    return;
                }

                if (pawn.Map != map)
                {
                    if (pawn.Map != null)
                    {
                        map = pawn.Map;
                    }
                    else
                    {
                        return;
                    }
                }

                Vector3 randomDirection = GetRandomDirection();

                if (Props.damage > 0)
                {
                    ApplyDamage(pawn);
                }

                ApplyRandomKnockback(pawn, randomDirection, map);

                if (!string.IsNullOrEmpty(Props.stunHediff))
                {
                    ApplyStunEffect(pawn);
                }

                AddHitEffect(pawn);

                if (Props.showMessages)
                {
                    Messages.Message($"{pawn.LabelShort} is whipped and flung from the ground!",
                        pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Error flinging pawn {pawn?.LabelShort}: {ex}");
            }
        }

        private Vector3 GetRandomDirection()
        {
            float randomAngle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }

        private void ApplyRandomKnockback(Pawn pawn, Vector3 direction, Map map)
        {
            try
            {
                if (pawn == null)
                {
                    return;
                }

                if (map == null)
                {
                    return;
                }

                if (!pawn.Spawned)
                {
                    return;
                }

                IntVec3 currentPos = pawn.Position;
                IntVec3 targetPos = currentPos;

                for (int i = 1; i <= Props.flingDistance; i++)
                {
                    IntVec3 testPos = currentPos + new IntVec3(
                        Mathf.RoundToInt(direction.x * i),
                        0,
                        Mathf.RoundToInt(direction.z * i)
                    );

                    if (testPos.InBounds(map) && testPos.Standable(map) && !testPos.Filled(map))
                    {
                        targetPos = testPos;
                    }
                    else
                    {
                        break;
                    }
                }

                if (targetPos != currentPos)
                {
                    if (pawn.Spawned)
                    {
                        pawn.Position = targetPos;
                        pawn.Notify_Teleported(false, false);

                        if (pawn.jobs?.curDriver != null)
                        {
                            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Ground Fling] Exception in ApplyRandomKnockback for {pawn?.LabelShort}: {ex}");
            }
        }

        private void AddGroundEffect(LocalTargetInfo target, Map map)
        {
            if (Props.groundEffectSound != null)
            {
                Props.groundEffectSound.PlayOneShot(new TargetInfo(target.Cell, map));
            }

            if (Props.groundEffecter != null)
            {
                try
                {
                    Effecter effecter = Props.groundEffecter.Spawn();
                    TargetInfo targetInfo = new TargetInfo(target.Cell, map);
                    effecter.Trigger(targetInfo, TargetInfo.Invalid);
                    effecter.Trigger(targetInfo, targetInfo);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Ground Fling] Exception in AddGroundEffect: {ex}");
                }
            }
        }

        private void AddHitEffect(Pawn pawn)
        {
            if (pawn?.Map == null)
                return;

            if (Props.hitSound != null)
            {
                Props.hitSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            if (Props.hitEffecter != null)
            {
                try
                {
                    Effecter effecter = Props.hitEffecter.Spawn();
                    effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Ground Fling] Error with hit effecter: {ex}");
                }
            }

            if (Props.hitEffecterCount > 0 && (Props.hitEffecter != null || Props.groundEffecter != null))
            {
                AddEffectsAroundTarget(pawn.Position, pawn.Map, Props.hitEffecter ?? Props.groundEffecter, Props.hitEffecterCount, Props.hitEffecterRadius);
            }
        }

        private void AddEffectsAroundTarget(IntVec3 center, Map map, EffecterDef effecterDef, int count, float radius)
        {
            if (map == null || effecterDef == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
                IntVec3 effectPos = center + new IntVec3(
                    Mathf.RoundToInt(randomCircle.x),
                    0,
                    Mathf.RoundToInt(randomCircle.y)
                );

                if (effectPos.InBounds(map))
                {
                    try
                    {
                        Effecter effecter = effecterDef.Spawn();
                        effecter.Trigger(new TargetInfo(effectPos, map), TargetInfo.Invalid);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Ground Fling] Error with additional effect {i + 1}: {ex}");
                    }
                }
            }
        }

        private void ApplyDamage(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            float damage = DamageScalingUtility.GetScaledDamage(
            Props.damage,
            parent.pawn,
            Props.scaleStat,
            Props.scaleSkill,
            Props.skillMultiplier,
            Props.debugScaling
        );

            DamageDef damageDef = Props.damageDef ?? DamageDefOf.Blunt;

            DamageInfo damageInfo = new DamageInfo(
                damageDef,
                Props.damage,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            pawn.TakeDamage(damageInfo);
        }

        private void ApplyStunEffect(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            try
            {
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.stunHediff);
                if (hediffDef == null)
                {
                    hediffDef = HediffDefOf.PsychicShock;
                }

                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                if (hediff != null)
                {
                    hediff.Severity = Props.stunSeverity;
                    pawn.health.AddHediff(hediff);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Failed to add stun effect to {pawn.LabelShort}: {ex}");
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!target.IsValid)
            {
                if (throwMessages)
                    Messages.Message("Invalid target", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (parent?.pawn?.Map == null)
            {
                if (throwMessages)
                    Messages.Message("No valid map", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (!target.Cell.InBounds(parent.pawn.Map))
            {
                if (throwMessages)
                    Messages.Message("Target out of bounds", MessageTypeDefOf.RejectInput);
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    public class CompProperties_AbilityEffect_GroundFling : CompProperties_AbilityEffect
    {
        public float effectRadius = 2.0f;

        public int maxTargets = 1;
        public bool affectCaster = false;
        public bool affectDowned = true;
        public bool onlyAffectHostiles = false;

        public int flingDistance = 4;

        public int damage = 10;
        public DamageDef damageDef = null;
        public float armorPenetration = 0f;

        public string stunHediff = "PsychicShock";
        public float stunSeverity = 1.0f;

        public SoundDef groundEffectSound = null;
        public EffecterDef groundEffecter = null;
        public SoundDef hitSound = null;
        public EffecterDef hitEffecter = null;

        public int hitEffecterCount = 0;
        public float hitEffecterRadius = 1.5f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;

        public bool showMessages = true;

        public CompProperties_AbilityEffect_GroundFling()
        {
            compClass = typeof(CompAbilityEffect_GroundFling);
        }
    }
}