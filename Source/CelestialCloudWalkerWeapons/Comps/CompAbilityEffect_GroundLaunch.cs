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
    public class CompAbilityEffect_GroundLaunch : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_GroundLaunch Props => (CompProperties_AbilityEffect_GroundLaunch)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (parent?.pawn?.Map == null)
            {
                Log.Error("AnimeArsenal: GroundLaunch - No valid map found");
                return;
            }

            Map map = parent.pawn.Map;

            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                Log.Error("AnimeArsenal: GroundLaunch - Invalid target cell");
                return;
            }

            List<Pawn> affectedPawns = GetPawnsInArea(target, map);

            if (!affectedPawns.Any())
            {
                if (Props.showMessages)
                {
                    Messages.Message("No targets in range.", MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            AddGroundEffects(target, map);

            foreach (Pawn pawn in affectedPawns)
            {
                if (pawn != null && !pawn.Dead)
                {
                    ApplyLaunchEffect(pawn, target);
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

        private void ApplyLaunchEffect(Pawn pawn, LocalTargetInfo originalTarget)
        {
            try
            {
                Vector3 launchDirection = CalculateLaunchDirection(pawn, originalTarget);

                if (Props.damage > 0)
                {
                    ApplyDamage(pawn);
                }

                if (Props.knockbackDistance > 0)
                {
                    ApplyKnockback(pawn, launchDirection);
                }

                if (!string.IsNullOrEmpty(Props.stunHediff))
                {
                    ApplyStunEffect(pawn);
                }

                AddPawnEffects(pawn);

                if (Props.showMessages)
                {
                    Messages.Message($"{pawn.LabelShort} is launched into the air!",
                        pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Error applying launch effect to {pawn?.LabelShort}: {ex}");
            }
        }

        private Vector3 CalculateLaunchDirection(Pawn pawn, LocalTargetInfo center)
        {
            Vector3 pawnPos = pawn.Position.ToVector3Shifted();
            Vector3 centerPos = center.Cell.ToVector3Shifted();

            Vector3 direction = (pawnPos - centerPos);

            if (direction.magnitude < 0.1f)
            {
                float randomAngle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
            }
            else
            {
                direction = direction.normalized;
            }

            return direction;
        }

        private void ApplyKnockback(Pawn pawn, Vector3 direction)
        {
            IntVec3 currentPos = pawn.Position;
            IntVec3 targetPos = currentPos;
            Map map = pawn.Map;

            for (int i = 1; i <= Props.knockbackDistance; i++)
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

                if (Props.knockbackEffecter != null)
                {
                    Effecter effecter = Props.knockbackEffecter.Spawn();
                    effecter.Trigger(new TargetInfo(targetPos, map), TargetInfo.Invalid);
                    effecter.Cleanup();
                }
            }
        }

        private void ApplyDamage(Pawn pawn)
        {
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
            try
            {
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.stunHediff);
                if (hediffDef == null)
                {
                    hediffDef = HediffDefOf.PsychicShock;
                    Log.Warning($"AnimeArsenal: Hediff '{Props.stunHediff}' not found. Using Unconscious as fallback.");
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

        private void AddPawnEffects(Pawn pawn)
        {
            if (Props.pawnHitSound != null)
            {
                Props.pawnHitSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            if (Props.pawnHitEffecter != null)
            {
                Effecter effecter = Props.pawnHitEffecter.Spawn();
                effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                effecter.Cleanup();
            }
        }

        private void AddGroundEffects(LocalTargetInfo target, Map map)
        {
            if (Props.groundEruptSound != null)
            {
                Props.groundEruptSound.PlayOneShot(new TargetInfo(target.Cell, map));
            }

            if (Props.groundEruptEffecter != null)
            {
                Effecter effecter = Props.groundEruptEffecter.Spawn();
                effecter.Trigger(new TargetInfo(target.Cell, map), TargetInfo.Invalid);
                effecter.Cleanup();
            }

            if (Props.showRadiusEffects && Props.radiusEffecter != null)
            {
                IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(target.Cell, Props.effectRadius, true);
                int effectCount = 0;

                foreach (IntVec3 cell in cells)
                {
                    if (effectCount >= Props.maxRadiusEffects) break;

                    if (cell.InBounds(map))
                    {
                        Effecter effecter = Props.radiusEffecter.Spawn();
                        effecter.Trigger(new TargetInfo(cell, map), TargetInfo.Invalid);
                        effecter.Cleanup();
                        effectCount++;
                    }
                }
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

    public class CompProperties_AbilityEffect_GroundLaunch : CompProperties_AbilityEffect
    {
        public float effectRadius = 3.0f;

        public int knockbackDistance = 3;
        public EffecterDef knockbackEffecter = null;

        public int damage = 10;
        public DamageDef damageDef = null;
        public float armorPenetration = 0f;

        public string stunHediff = "PsychicShock ";
        public float stunSeverity = 1.0f;

        public bool affectCaster = false;
        public bool affectDowned = true;
        public bool onlyAffectHostiles = false;

        public SoundDef groundEruptSound = null;
        public EffecterDef groundEruptEffecter = null;
        public SoundDef pawnHitSound = null;
        public EffecterDef pawnHitEffecter = null;

        public bool showRadiusEffects = false;
        public EffecterDef radiusEffecter = null;
        public int maxRadiusEffects = 10;

        public bool showMessages = true;

        public CompProperties_AbilityEffect_GroundLaunch()
        {
            compClass = typeof(CompAbilityEffect_GroundLaunch);
        }
    }
}