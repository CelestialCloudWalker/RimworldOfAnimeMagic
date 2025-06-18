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

            // Safety check for map
            if (parent?.pawn?.Map == null)
            {
                Log.Error("AnimeArsenal: GroundLaunch - No valid map found");
                return;
            }

            Map map = parent.pawn.Map;

            // Validate target cell
            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                Log.Error("AnimeArsenal: GroundLaunch - Invalid target cell");
                return;
            }

            // Get all pawns in the target area
            List<Pawn> affectedPawns = GetPawnsInArea(target, map);

            if (!affectedPawns.Any())
            {
                if (Props.showMessages)
                {
                    Messages.Message("No targets in range.", MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            // Add ground effects first for visual impact
            AddGroundEffects(target, map);

            // Apply effects to each pawn
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

            // Get all cells in the effect radius
            IEnumerable<IntVec3> cells = GenRadial.RadialCellsAround(target.Cell, Props.effectRadius, true);

            foreach (IntVec3 cell in cells)
            {
                if (cell.InBounds(map))
                {
                    // Find pawns in each cell
                    List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                    foreach (Thing thing in things)
                    {
                        if (thing is Pawn pawn && !pawns.Contains(pawn))
                        {
                            // Check if pawn should be affected
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
            // Don't affect the caster if specified
            if (!Props.affectCaster && pawn == parent.pawn)
                return false;

            // Don't affect downed pawns if specified
            if (!Props.affectDowned && pawn.Downed)
                return false;

            // Don't affect dead pawns
            if (pawn.Dead || pawn.Destroyed)
                return false;

            // Check faction relations if specified
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
                // Calculate launch direction (away from center)
                Vector3 launchDirection = CalculateLaunchDirection(pawn, originalTarget);

                // Apply damage first (before knockback)
                if (Props.damage > 0)
                {
                    ApplyDamage(pawn);
                }

                // Apply knockback
                if (Props.knockbackDistance > 0)
                {
                    ApplyKnockback(pawn, launchDirection);
                }

                // Apply stun/hediff after knockback
                if (!string.IsNullOrEmpty(Props.stunHediff))
                {
                    ApplyStunEffect(pawn);
                }

                // Add individual pawn effects
                AddPawnEffects(pawn);

                // Show message
                if (Props.showMessages)
                {
                    Messages.Message($"{pawn.LabelShort} is launched into the air!",
                        pawn, MessageTypeDefOf.NeutralEvent);
                }

                Log.Message($"[Ground Launch] Applied launch effect to {pawn.LabelShort}");
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

            // Calculate direction away from center
            Vector3 direction = (pawnPos - centerPos);

            // If pawn is at exact center, use random direction
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
            // Calculate target position
            IntVec3 currentPos = pawn.Position;
            IntVec3 targetPos = currentPos;
            Map map = pawn.Map;

            // Try to find a valid position to knock back to
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
                    break; // Stop if we hit an obstacle
                }
            }

            // Only move if we found a different position
            if (targetPos != currentPos)
            {
                // Use safer position setting
                if (pawn.Spawned)
                {
                    pawn.Position = targetPos;
                    pawn.Notify_Teleported(false, false);

                    // Update pawn's location for AI
                    if (pawn.jobs?.curDriver != null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }

                // Add knockback visual effect
                if (Props.knockbackEffecter != null)
                {
                    Effecter effecter = Props.knockbackEffecter.Spawn();
                    effecter.Trigger(new TargetInfo(targetPos, map), TargetInfo.Invalid);
                    effecter.Cleanup();
                }

                Log.Message($"[Ground Launch] Knocked {pawn.LabelShort} from {currentPos} to {targetPos}");
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
                parent.pawn, // instigator
                null, // hit part
                null, // weapon
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            pawn.TakeDamage(damageInfo);
            Log.Message($"[Ground Launch] Applied {Props.damage} {damageDef.label} damage to {pawn.LabelShort}");
        }

        private void ApplyStunEffect(Pawn pawn)
        {
            try
            {
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.stunHediff);
                if (hediffDef == null)
                {
                    // Use vanilla Unconscious as fallback - this is guaranteed to exist
                    hediffDef = HediffDefOf.PsychicShock;
                    Log.Warning($"AnimeArsenal: Hediff '{Props.stunHediff}' not found. Using Unconscious as fallback.");
                }

                // Remove existing hediff if present
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

                    Log.Message($"[Ground Launch] Applied {hediffDef.label} effect to {pawn.LabelShort}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Failed to add stun effect to {pawn.LabelShort}: {ex}");
            }
        }

        private void AddPawnEffects(Pawn pawn)
        {
            // Play individual pawn sound
            if (Props.pawnHitSound != null)
            {
                Props.pawnHitSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            // Add individual pawn visual effect
            if (Props.pawnHitEffecter != null)
            {
                Effecter effecter = Props.pawnHitEffecter.Spawn();
                effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                effecter.Cleanup();
            }
        }

        private void AddGroundEffects(LocalTargetInfo target, Map map)
        {
            // Play ground eruption sound
            if (Props.groundEruptSound != null)
            {
                Props.groundEruptSound.PlayOneShot(new TargetInfo(target.Cell, map));
            }

            // Add ground eruption visual effect
            if (Props.groundEruptEffecter != null)
            {
                Effecter effecter = Props.groundEruptEffecter.Spawn();
                effecter.Trigger(new TargetInfo(target.Cell, map), TargetInfo.Invalid);
                effecter.Cleanup();
            }

            // Create additional visual effects in radius if desired
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
        // Area of effect
        public float effectRadius = 3.0f;

        // Knockback properties
        public int knockbackDistance = 3;
        public EffecterDef knockbackEffecter = null;

        // Damage properties  
        public int damage = 10;
        public DamageDef damageDef = null; // Will default to Blunt if null
        public float armorPenetration = 0f;

        // Stun properties (using string to avoid XML reference issues)
        public string stunHediff = "PsychicShock "; // Default to vanilla PsychicShock  hediff
        public float stunSeverity = 1.0f;

        // Targeting options
        public bool affectCaster = false;
        public bool affectDowned = true;
        public bool onlyAffectHostiles = false;

        // Visual/Audio effects
        public SoundDef groundEruptSound = null;
        public EffecterDef groundEruptEffecter = null;
        public SoundDef pawnHitSound = null;
        public EffecterDef pawnHitEffecter = null;

        // Radius effects
        public bool showRadiusEffects = false;
        public EffecterDef radiusEffecter = null;
        public int maxRadiusEffects = 10;

        // Messages
        public bool showMessages = true;

        public CompProperties_AbilityEffect_GroundLaunch()
        {
            compClass = typeof(CompAbilityEffect_GroundLaunch);
        }
    }
}