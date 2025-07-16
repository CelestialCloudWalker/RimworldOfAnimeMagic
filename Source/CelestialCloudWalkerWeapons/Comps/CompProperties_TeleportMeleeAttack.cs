using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace AnimeArsenal
{
    public class CompProperties_TeleportMeleeAttack : CompProperties_AbilityEffect
    {
        
        public int damageAmount = 15;
        public DamageDef damageType; 
        public float armorPenetration = 0.3f;
        public int stunDuration = 0;
        public float effectRadius = 0f;
        public float additionalDamageFactorFromMeleeSkill = 0f;

        public EffecterDef casterEffecter;  
        public EffecterDef impactEffecter;  

        public CompProperties_TeleportMeleeAttack()
        {
            compClass = typeof(CompAbilityEffect_TeleportMeleeAttack);
        }

    }

    public class CompAbilityEffect_TeleportMeleeAttack : CompAbilityEffect
    {
        private Thing TargetThing;
        private bool teleportQueued = false;
        private IntVec3 queuedDestination;

        private new CompProperties_TeleportMeleeAttack Props => (CompProperties_TeleportMeleeAttack)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!target.HasThing || target.Thing == null || target.Thing.Map == null || parent?.pawn == null)
            {
                Log.Warning("TeleportMeleeAttack - Invalid target or state");
                return;
            }

            if (Props.casterEffecter != null)
            {
                Effecter effecter = Props.casterEffecter.Spawn();
                effecter.Trigger(new TargetInfo(parent.pawn.Position, parent.pawn.Map), new TargetInfo(parent.pawn.Position, parent.pawn.Map));
                effecter.Cleanup();
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

                        if (Props.effectRadius <= 0f)
                        {
                            ApplyDamageToTarget(TargetThing);
                        }
                        else
                        {
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

            int finalDamage = Props.damageAmount;
            if (Props.additionalDamageFactorFromMeleeSkill > 0 && parent.pawn != null)
            {
                int meleeSkill = parent.pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                finalDamage += (int)(meleeSkill * Props.additionalDamageFactorFromMeleeSkill * Props.damageAmount);
            }

            DamageDef damageToUse = Props.damageType ?? DamageDefOf.Cut;

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

            target.TakeDamage(dinfo);

            if (Props.impactEffecter != null)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(target.Position, target.Map), new TargetInfo(target.Position, target.Map));
                effecter.Cleanup();
            }

            if (Props.stunDuration > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances?.stunner?.StunFor(Props.stunDuration, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map)
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, Props.effectRadius, true))
            {
                if (thing != parent.pawn) 
                {
                    ApplyDamageToTarget(thing);
                }
            }
        }
    }
}