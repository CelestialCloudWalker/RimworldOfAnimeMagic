using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace AnimeArsenal
{
    public class CompProperties_TeleportMeleeAttack : CompProperties_AbilityEffect
    {
        // Damage parameters
        public int damageAmount = 15;
        public DamageDef damageType; // No default initialization, will be set by XML
        public float armorPenetration = 0.3f;
        public int stunDuration = 0;
        public float effectRadius = 0f;
        public float additionalDamageFactorFromMeleeSkill = 0f;

        public CompProperties_TeleportMeleeAttack()
        {
            compClass = typeof(CompAbilityEffect_TeleportMeleeAttack);
        }

        // No ResolveReferences method needed if damageType is always set in XML
    }

    // Rest of your code remains unchanged
    public class CompAbilityEffect_TeleportMeleeAttack : CompAbilityEffect
    {
        private Thing TargetThing;
        private bool teleportQueued = false;
        private IntVec3 queuedDestination;

        // Accessor for the properties
        private new CompProperties_TeleportMeleeAttack Props => (CompProperties_TeleportMeleeAttack)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!target.HasThing || target.Thing == null || target.Thing.Map == null || parent?.pawn == null)
            {
                Log.Warning("TeleportMeleeAttack - Invalid target or state");
                return;
            }
            TargetThing = target.Thing;
            queuedDestination = TargetThing.Position;
            teleportQueued = true;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (teleportQueued)
            {
                teleportQueued = false;
                ExecuteTeleport();
            }
        }

        private void ExecuteTeleport()
        {
            if (TargetThing == null || parent?.pawn == null || TargetThing.Map == null)
            {
                return;
            }
            try
            {
                PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_TPStrikeFlyer, parent.pawn, queuedDestination, null, null);
                if (pawnFlyer != null)
                {
                    DelegateFlyer flyer = GenSpawn.Spawn(pawnFlyer, queuedDestination, TargetThing.Map) as DelegateFlyer;
                    if (flyer != null)
                    {
                        flyer.OnRespawnPawn += Flyer_OnRespawnPawn;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"TeleportMeleeAttack - Error during teleport: {ex}");
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.HasThing && target.Thing != null;
        }

        private void Flyer_OnRespawnPawn(Pawn pawn, PawnFlyer flyer, Map map)
        {
            if (flyer is DelegateFlyer delegateFlyer)
            {
                delegateFlyer.OnRespawnPawn -= Flyer_OnRespawnPawn;
            }
            if (TargetThing == null || parent?.pawn == null || TargetThing.Map == null)
            {
                return;
            }
            try
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    IntVec3 adjacentCell = TargetThing.RandomAdjacentCell8Way();
                    if (adjacentCell.InBounds(map))
                    {
                        parent.pawn.Position = adjacentCell;

                        // Apply custom damage instead of using standard melee attack
                        if (Props.effectRadius <= 0f)
                        {
                            // Single target damage
                            ApplyDamageToTarget(TargetThing);
                        }
                        else
                        {
                            // AOE damage
                            ApplyAreaDamage(TargetThing.Position, map);
                        }
                    }
                });
            }
            catch (System.Exception ex)
            {
                Log.Error($"TeleportMeleeAttack - Error during respawn: {ex}");
            }
        }

        private void ApplyDamageToTarget(Thing target)
        {
            if (target == null) return;

            // Calculate damage based on melee skill if configured
            int finalDamage = Props.damageAmount;
            if (Props.additionalDamageFactorFromMeleeSkill > 0 && parent.pawn != null)
            {
                int meleeSkill = parent.pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                finalDamage += (int)(meleeSkill * Props.additionalDamageFactorFromMeleeSkill * Props.damageAmount);
            }

            // Check if damageType is null and handle it
            DamageDef damageToUse = Props.damageType ?? DamageDefOf.Cut;

            // Create damage info
            DamageInfo dinfo = new DamageInfo(
                damageToUse,
                finalDamage,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown,
                target);

            // Apply damage
            target.TakeDamage(dinfo);

            // Apply stun if configured
            if (Props.stunDuration > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances?.stunner?.StunFor(Props.stunDuration, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map)
        {
            // Find all pawns in radius
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, Props.effectRadius, true))
            {
                if (thing != parent.pawn) // Don't damage yourself
                {
                    ApplyDamageToTarget(thing);
                }
            }
        }
    }
}