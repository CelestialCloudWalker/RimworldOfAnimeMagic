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

            if (parent?.pawn?.Map == null)
            {
                Log.Error("AnimeArsenal: GroundFling - No valid map found");
                return;
            }

            Map map = parent.pawn.Map;
            Log.Message($"[Ground Fling] Map found: {map}");

            if (!target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                Log.Error($"AnimeArsenal: GroundFling - Invalid target cell: {target.Cell}, Valid: {target.Cell.IsValid}, InBounds: {target.Cell.InBounds(map)}");
                return;
            }

            Log.Message($"[Ground Fling] Target cell validated: {target.Cell}");

            Log.Message("[Ground Fling] Testing ground effect FIRST");
            AddGroundEffect(target, map);

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

            if (Props.maxTargets > 0 && affectedPawns.Count > Props.maxTargets)
            {
                affectedPawns = affectedPawns.InRandomOrder().Take(Props.maxTargets).ToList();
                Log.Message($"[Ground Fling] Limited to {affectedPawns.Count} pawns");
            }

            foreach (Pawn pawn in affectedPawns)
            {
                if (pawn != null && !pawn.Dead)
                {
                    Log.Message($"[Ground Fling] Flinging pawn: {pawn.LabelShort}");
                    FlingPawn(pawn, map);
                }
            }

            Log.Message("[Ground Fling] Apply method completed");
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
                    Log.Warning($"[Ground Fling] Attempted to fling invalid pawn: {pawn?.LabelShort ?? "null"}");
                    return;
                }

                if (map == null)
                {
                    Log.Error($"[Ground Fling] Map is null when trying to fling {pawn.LabelShort}");
                    return;
                }

                if (pawn.Map != map)
                {
                    Log.Warning($"[Ground Fling] Pawn {pawn.LabelShort} is on different map than expected. Pawn map: {pawn.Map}, Expected map: {map}");
                    
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

                Log.Message($"[Ground Fling] Whipped and flung {pawn.LabelShort} in random direction");
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

                    Log.Message("[Ground Fling] Trying Approach 1: Simple spawn and trigger");
                    Effecter effecter = Props.groundEffecter.Spawn();
                    Log.Message($"[Ground Fling] Effecter spawned successfully: {effecter}");

                    TargetInfo targetInfo = new TargetInfo(target.Cell, map);
                    Log.Message($"[Ground Fling] TargetInfo created: {targetInfo.IsValid}, Cell: {targetInfo.Cell}, Map: {targetInfo.Map}");

                    effecter.Trigger(targetInfo, TargetInfo.Invalid);
                    Log.Message("[Ground Fling] Approach 1: Effecter triggered");

                    Log.Message("[Ground Fling] Trying Approach 2: Both targets same");
                    effecter.Trigger(targetInfo, targetInfo);
                    Log.Message("[Ground Fling] Approach 2: Effecter triggered with both targets");

                    Log.Message("[Ground Fling] Trying Approach 3: Manual cleanup");
                    effecter.Cleanup();
                    Log.Message("[Ground Fling] Approach 3: Effecter cleanup called");

                    Log.Message("[Ground Fling] Trying Approach 4: Fresh spawn");
                    Effecter effecter2 = Props.groundEffecter.Spawn(target.Cell, map);
                    Log.Message($"[Ground Fling] Effecter2 spawned with position: {effecter2}");

                    effecter2.Trigger(targetInfo, TargetInfo.Invalid);
                    Log.Message("[Ground Fling] Approach 4: Effecter2 triggered");

                }
                catch (Exception ex)
                {
                    Log.Error($"[Ground Fling] Exception in AddGroundEffect: {ex}");

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

                Log.Message($"[Ground Fling] Props type: {Props.GetType()}");
                Log.Message($"[Ground Fling] Props.effectRadius: {Props.effectRadius}");
                Log.Message($"[Ground Fling] Props.maxTargets: {Props.maxTargets}");
            }

            Log.Message($"[Ground Fling] AddGroundEffect END");
        }

        private void AddHitEffect(Pawn pawn)
        {
            Log.Message($"[Ground Fling] AddHitEffect START for {pawn.LabelShort}");

            if (pawn?.Map == null)
            {
                Log.Warning($"[Ground Fling] Cannot add hit effect for {pawn?.LabelShort} - pawn or map is null");
                return;
            }

            if (Props.hitSound != null)
            {
                Log.Message($"[Ground Fling] Playing hit sound: {Props.hitSound.defName}");
                Props.hitSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
            else
            {
                Log.Message("[Ground Fling] No hitSound defined");
            }

            EffecterDef effecterToUse = Props.hitEffecter ?? Props.groundEffecter;
            Log.Message($"[Ground Fling] EffecterToUse: {effecterToUse?.defName ?? "NULL"}");

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
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
                IntVec3 effectPos = center + new IntVec3(
                    Mathf.RoundToInt(randomCircle.x),
                    0,
                    Mathf.RoundToInt(randomCircle.y)
                );

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
                parent.pawn, 
                null, 
                null, 
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
                    
                    hediffDef = HediffDefOf.PsychicShock;
                    Log.Warning($"AnimeArsenal: Hediff '{Props.stunHediff}' not found. Using PsychicShock as fallback.");
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

        
        public bool showMessages = true;

        public CompProperties_AbilityEffect_GroundFling()
        {
            compClass = typeof(CompAbilityEffect_GroundFling);
        }
    }
}