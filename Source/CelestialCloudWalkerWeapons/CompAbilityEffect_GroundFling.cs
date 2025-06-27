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
            Log.Message($"[Ground Fling] Apply called - Target: {target.Cell}, Dest: {dest.Cell}");

            base.Apply(target, dest);

            // Safety check for map
            if (parent?.pawn?.Map == null)
            {
                Log.Error("AnimeArsenal: GroundFling - No valid map found");
                return;
            }

            Map map = parent.pawn.Map;
            Log.Message($"[Ground Fling] Map found: {map}");

            // Validate target cell
            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                Log.Error($"AnimeArsenal: GroundFling - Invalid target cell: {target.Cell}, Valid: {target.Cell.IsValid}, InBounds: {target.Cell.InBounds(map)}");
                return;
            }

            Log.Message($"[Ground Fling] Target cell validated: {target.Cell}");

            // FIRST: Test ground effect immediately
            Log.Message("[Ground Fling] Testing ground effect FIRST");
            AddGroundEffect(target, map);

            // Get all pawns in the target area
            List<Pawn> affectedPawns = GetPawnsInArea(target, map);
            Log.Message($"[Ground Fling] Found {affectedPawns.Count} affected pawns");

            if (!affectedPawns.Any())
            {
                Log.Message("[Ground Fling] No pawns found, but ground effect should still show");
                if (Props.showMessages)
                {
                    Messages.Message("No targets in range.", MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            // Limit to maximum targets if specified
            if (Props.maxTargets > 0 && affectedPawns.Count > Props.maxTargets)
            {
                affectedPawns = affectedPawns.InRandomOrder().Take(Props.maxTargets).ToList();
                Log.Message($"[Ground Fling] Limited to {affectedPawns.Count} pawns");
            }

            // Fling each pawn
            foreach (Pawn pawn in affectedPawns)
            {
                if (pawn != null && !pawn.Dead)
                {
                    Log.Message($"[Ground Fling] Flinging pawn: {pawn.LabelShort}");
                    FlingPawn(pawn, map); // Pass the map parameter
                }
            }

            Log.Message("[Ground Fling] Apply method completed");
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

        private void FlingPawn(Pawn pawn, Map map)
        {
            try
            {
                // Additional safety checks
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    Log.Warning($"[Ground Fling] Attempted to fling invalid pawn: {pawn?.LabelShort ?? "null"}");
                    return;
                }

                if (map == null)
                {
                    Log.Error($"[Ground Fling] Map is null when trying to fling {pawn.LabelShort}");
                    return;
                }

                // Verify pawn is still on the expected map
                if (pawn.Map != map)
                {
                    Log.Warning($"[Ground Fling] Pawn {pawn.LabelShort} is on different map than expected. Pawn map: {pawn.Map}, Expected map: {map}");
                    // Use the pawn's actual map if it exists
                    if (pawn.Map != null)
                    {
                        map = pawn.Map;
                    }
                    else
                    {
                        Log.Error($"[Ground Fling] Pawn {pawn.LabelShort} has no map, cannot fling");
                        return;
                    }
                }

                // Generate random direction
                Vector3 randomDirection = GetRandomDirection();

                // Apply damage first (before knockback)
                if (Props.damage > 0)
                {
                    ApplyDamage(pawn);
                }

                // Apply knockback in random direction
                ApplyRandomKnockback(pawn, randomDirection, map);

                // Apply stun effect after knockback
                if (!string.IsNullOrEmpty(Props.stunHediff))
                {
                    ApplyStunEffect(pawn);
                }

                // Add hit effect on the pawn
                AddHitEffect(pawn);

                // Show message
                if (Props.showMessages)
                {
                    Messages.Message($"{pawn.LabelShort} is whipped and flung from the ground!",
                        pawn, MessageTypeDefOf.NeutralEvent);
                }

                Log.Message($"[Ground Fling] Whipped and flung {pawn.LabelShort} in random direction");
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Error flinging pawn {pawn?.LabelShort}: {ex}");
            }
        }

        private Vector3 GetRandomDirection()
        {
            // Generate random angle
            float randomAngle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle));
        }

        private void ApplyRandomKnockback(Pawn pawn, Vector3 direction, Map map)
        {
            try
            {
                // Additional null checks
                if (pawn == null)
                {
                    Log.Error("[Ground Fling] Pawn is null in ApplyRandomKnockback");
                    return;
                }

                if (map == null)
                {
                    Log.Error($"[Ground Fling] Map is null in ApplyRandomKnockback for pawn {pawn.LabelShort}");
                    return;
                }

                if (!pawn.Spawned)
                {
                    Log.Warning($"[Ground Fling] Pawn {pawn.LabelShort} is not spawned, cannot apply knockback");
                    return;
                }

                IntVec3 currentPos = pawn.Position;
                IntVec3 targetPos = currentPos;

                Log.Message($"[Ground Fling] Applying knockback to {pawn.LabelShort} from {currentPos}");

                // Try to find a valid position to knock back to
                for (int i = 1; i <= Props.flingDistance; i++)
                {
                    IntVec3 testPos = currentPos + new IntVec3(
                        Mathf.RoundToInt(direction.x * i),
                        0,
                        Mathf.RoundToInt(direction.z * i)
                    );

                    // Check if position is valid
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

                    Log.Message($"[Ground Fling] Moved {pawn.LabelShort} from {currentPos} to {targetPos}");
                }
                else
                {
                    Log.Message($"[Ground Fling] Could not find valid knockback position for {pawn.LabelShort}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Ground Fling] Exception in ApplyRandomKnockback for {pawn?.LabelShort}: {ex}");
            }
        }

        private void AddGroundEffect(LocalTargetInfo target, Map map)
        {
            Log.Message($"[Ground Fling] AddGroundEffect START");
            Log.Message($"[Ground Fling] Target: {target.Cell}");
            Log.Message($"[Ground Fling] Map: {map}");
            Log.Message($"[Ground Fling] Props: {Props}");
            Log.Message($"[Ground Fling] Props.groundEffecter: {Props.groundEffecter}");

            // Play ground effect sound
            if (Props.groundEffectSound != null)
            {
                Log.Message($"[Ground Fling] Playing sound: {Props.groundEffectSound.defName}");
                Props.groundEffectSound.PlayOneShot(new TargetInfo(target.Cell, map));
            }
            else
            {
                Log.Message("[Ground Fling] No groundEffectSound defined");
            }

            if (Props.groundEffecter != null)
            {
                Log.Message($"[Ground Fling] Found groundEffecter: {Props.groundEffecter.defName}");

                try
                {
                    // Try multiple approaches for triggering the effecter

                    // Approach 1: Simple spawn and trigger (what we had before)
                    Log.Message("[Ground Fling] Trying Approach 1: Simple spawn and trigger");
                    Effecter effecter = Props.groundEffecter.Spawn();
                    Log.Message($"[Ground Fling] Effecter spawned successfully: {effecter}");

                    TargetInfo targetInfo = new TargetInfo(target.Cell, map);
                    Log.Message($"[Ground Fling] TargetInfo created: {targetInfo.IsValid}, Cell: {targetInfo.Cell}, Map: {targetInfo.Map}");

                    effecter.Trigger(targetInfo, TargetInfo.Invalid);
                    Log.Message("[Ground Fling] Approach 1: Effecter triggered");

                    // Approach 2: Try with both targets being the same
                    Log.Message("[Ground Fling] Trying Approach 2: Both targets same");
                    effecter.Trigger(targetInfo, targetInfo);
                    Log.Message("[Ground Fling] Approach 2: Effecter triggered with both targets");

                    // Approach 3: Manual cleanup after a delay (some effecters need this)
                    Log.Message("[Ground Fling] Trying Approach 3: Manual cleanup");
                    effecter.Cleanup();
                    Log.Message("[Ground Fling] Approach 3: Effecter cleanup called");

                    // Approach 4: Try spawning a new effecter with different parameters
                    Log.Message("[Ground Fling] Trying Approach 4: Fresh spawn");
                    Effecter effecter2 = Props.groundEffecter.Spawn(target.Cell, map);
                    Log.Message($"[Ground Fling] Effecter2 spawned with position: {effecter2}");

                    effecter2.Trigger(targetInfo, TargetInfo.Invalid);
                    Log.Message("[Ground Fling] Approach 4: Effecter2 triggered");

                }
                catch (Exception ex)
                {
                    Log.Error($"[Ground Fling] Exception in AddGroundEffect: {ex}");

                    // Fallback: Try the most basic approach
                    try
                    {
                        Log.Message("[Ground Fling] Trying fallback approach");
                        Effecter fallbackEffecter = Props.groundEffecter.Spawn();
                        fallbackEffecter.Trigger(new TargetInfo(target.Cell, map), new TargetInfo(target.Cell, map));
                        Log.Message("[Ground Fling] Fallback approach succeeded");
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error($"[Ground Fling] Fallback approach also failed: {fallbackEx}");
                    }
                }
            }
            else
            {
                Log.Error("[Ground Fling] Props.groundEffecter is NULL!");

                // Debug Props contents
                Log.Message($"[Ground Fling] Props type: {Props.GetType()}");
                Log.Message($"[Ground Fling] Props.effectRadius: {Props.effectRadius}");
                Log.Message($"[Ground Fling] Props.maxTargets: {Props.maxTargets}");
            }

            Log.Message($"[Ground Fling] AddGroundEffect END");
        }

        private void AddHitEffect(Pawn pawn)
        {
            Log.Message($"[Ground Fling] AddHitEffect START for {pawn.LabelShort}");

            // Additional safety check
            if (pawn?.Map == null)
            {
                Log.Warning($"[Ground Fling] Cannot add hit effect for {pawn?.LabelShort} - pawn or map is null");
                return;
            }

            // Play hit sound
            if (Props.hitSound != null)
            {
                Log.Message($"[Ground Fling] Playing hit sound: {Props.hitSound.defName}");
                Props.hitSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
            else
            {
                Log.Message("[Ground Fling] No hitSound defined");
            }

            // Determine which effecter to use for additional spawns
            EffecterDef effecterToUse = Props.hitEffecter ?? Props.groundEffecter;
            Log.Message($"[Ground Fling] EffecterToUse: {effecterToUse?.defName ?? "NULL"}");

            // Add hit visual effect at pawn location if hitEffecter is specified
            if (Props.hitEffecter != null)
            {
                Log.Message($"[Ground Fling] Spawning hitEffecter: {Props.hitEffecter.defName}");
                try
                {
                    Effecter effecter = Props.hitEffecter.Spawn();
                    effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                    Log.Message($"[Ground Fling] Hit effecter triggered successfully");
                }
                catch (Exception ex)
                {
                    Log.Error($"[Ground Fling] Error with hit effecter: {ex}");
                }
            }
            else
            {
                Log.Message("[Ground Fling] No hitEffecter defined");
            }

            // Add additional effects around the pawn if configured
            if (Props.hitEffecterCount > 0 && effecterToUse != null)
            {
                Log.Message($"[Ground Fling] Adding {Props.hitEffecterCount} additional effects around {pawn.LabelShort}");
                AddEffectsAroundTarget(pawn.Position, pawn.Map, effecterToUse, Props.hitEffecterCount, Props.hitEffecterRadius);
            }
            else
            {
                Log.Message($"[Ground Fling] Not adding additional effects - Count: {Props.hitEffecterCount}, EffecterToUse: {effecterToUse?.defName ?? "NULL"}");
            }

            Log.Message($"[Ground Fling] AddHitEffect END");
        }

        private void AddEffectsAroundTarget(IntVec3 center, Map map, EffecterDef effecterDef, int count, float radius)
        {
            Log.Message($"[Ground Fling] AddEffectsAroundTarget - Center: {center}, Count: {count}, Radius: {radius}");

            if (map == null)
            {
                Log.Error("[Ground Fling] Map is null in AddEffectsAroundTarget");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                // Generate random position within radius
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
                IntVec3 effectPos = center + new IntVec3(
                    Mathf.RoundToInt(randomCircle.x),
                    0,
                    Mathf.RoundToInt(randomCircle.y)
                );

                // Make sure position is valid
                if (effectPos.InBounds(map))
                {
                    Log.Message($"[Ground Fling] Spawning additional effect {i + 1} at {effectPos}");
                    try
                    {
                        Effecter effecter = effecterDef.Spawn();
                        effecter.Trigger(new TargetInfo(effectPos, map), TargetInfo.Invalid);
                        Log.Message($"[Ground Fling] Additional effect {i + 1} triggered successfully");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Ground Fling] Error with additional effect {i + 1}: {ex}");
                    }
                }
                else
                {
                    Log.Message($"[Ground Fling] Skipping additional effect {i + 1} - position {effectPos} out of bounds");
                }
            }
        }

        private void ApplyDamage(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                Log.Warning("[Ground Fling] Cannot apply damage to null or dead pawn");
                return;
            }

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
            Log.Message($"[Ground Fling] Applied {Props.damage} {damageDef.label} damage to {pawn.LabelShort}");
        }

        private void ApplyStunEffect(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
            {
                Log.Warning("[Ground Fling] Cannot apply stun effect to null or dead pawn");
                return;
            }

            try
            {
                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(Props.stunHediff);
                if (hediffDef == null)
                {
                    // Use vanilla PsychicShock as fallback
                    hediffDef = HediffDefOf.PsychicShock;
                    Log.Warning($"AnimeArsenal: Hediff '{Props.stunHediff}' not found. Using PsychicShock as fallback.");
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

                    Log.Message($"[Ground Fling] Applied {hediffDef.label} effect to {pawn.LabelShort}");
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
        // Area of effect
        public float effectRadius = 2.0f;

        // Targeting options
        public int maxTargets = 1; // Maximum number of pawns to affect (0 = unlimited)
        public bool affectCaster = false;
        public bool affectDowned = true;
        public bool onlyAffectHostiles = false;

        // Fling properties
        public int flingDistance = 4;

        // Damage properties  
        public int damage = 10;
        public DamageDef damageDef = null; // Will default to Blunt if null
        public float armorPenetration = 0f;

        // Stun properties (using string to avoid XML reference issues)
        public string stunHediff = "PsychicShock"; // Default to vanilla PsychicShock hediff
        public float stunSeverity = 1.0f;

        // Effects
        public SoundDef groundEffectSound = null;
        public EffecterDef groundEffecter = null;
        public SoundDef hitSound = null;
        public EffecterDef hitEffecter = null;

        // Additional hit effects around target
        public int hitEffecterCount = 0; // Number of additional effects to spawn around each hit pawn
        public float hitEffecterRadius = 1.5f; // Radius around pawn to spawn additional effects

        // Messages
        public bool showMessages = true;

        public CompProperties_AbilityEffect_GroundFling()
        {
            compClass = typeof(CompAbilityEffect_GroundFling);
        }
    }
}