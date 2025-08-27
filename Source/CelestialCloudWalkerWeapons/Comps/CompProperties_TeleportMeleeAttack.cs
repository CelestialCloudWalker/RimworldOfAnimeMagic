using RimWorld;
using Verse;
using System.Collections.Generic;
using System;
using Verse.AI;

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
        private bool effecterPlayed = false;
        private bool damageApplied = false;

        private new CompProperties_TeleportMeleeAttack Props => (CompProperties_TeleportMeleeAttack)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!target.HasThing || target.Thing == null || target.Thing.Map == null || parent?.pawn == null)
            {
                Log.Warning("TeleportMeleeAttack - Invalid target or state");
                return;
            }

            effecterPlayed = false;
            damageApplied = false;

            if (Props.casterEffecter != null)
            {
                Effecter effecter = Props.casterEffecter.Spawn();
                effecter.Trigger(new TargetInfo(parent.pawn.Position, parent.pawn.Map), new TargetInfo(parent.pawn.Position, parent.pawn.Map));
                effecter.Cleanup();
            }

            TargetThing = target.Thing;
            IntVec3 destination = TargetThing.Position;

            ExecuteTeleport(destination);
        }

        private void ExecuteTeleport(IntVec3 destination)
        {
            if (TargetThing == null || parent?.pawn == null || TargetThing.Map == null)
            {
                return;
            }
            try
            {
                PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_TPStrikeFlyer, parent.pawn, destination, null, null);
                if (pawnFlyer != null)
                {
                    DelegateFlyer flyer = GenSpawn.Spawn(pawnFlyer, destination, TargetThing.Map) as DelegateFlyer;
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

            if (damageApplied)
            {
                Log.Warning("TeleportMeleeAttack - Damage already applied, skipping");
                return;
            }

            try
            {
                IntVec3 adjacentCell = TargetThing.RandomAdjacentCell8Way();
                if (adjacentCell.InBounds(map) && adjacentCell.Standable(map))
                {
                    parent.pawn.Position = adjacentCell;

                    if (parent.pawn.stances != null)
                    {
                        parent.pawn.stances.SetStance(new Stance_Mobile());
                    }

                    if (Props.effectRadius <= 0f)
                    {
                        ApplyDamageToTarget(TargetThing, true);
                    }
                    else
                    {
                        ApplyAreaDamage(TargetThing.Position, map);
                    }

                    damageApplied = true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"TeleportMeleeAttack - Error during respawn: {ex}");

                try
                {
                    if (parent.pawn?.stances != null)
                    {
                        parent.pawn.stances.SetStance(new Stance_Mobile());
                    }
                }
                catch {  }
            }
        }

        private void ApplyDamageToTarget(Thing target, bool playImpactEffecter = false)
        {
            if (target == null || damageApplied) return;

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

            if (playImpactEffecter && Props.impactEffecter != null && !effecterPlayed)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(target.Position, target.Map), new TargetInfo(target.Position, target.Map));
                effecter.Cleanup();
                effecterPlayed = true;
            }

            if (Props.stunDuration > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances?.stunner?.StunFor(Props.stunDuration, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map)
        {
            if (damageApplied) return;

            List<Thing> affectedThings = new List<Thing>();

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, Props.effectRadius, true))
            {
                if (thing != parent.pawn && (thing is Pawn || thing.def.useHitPoints))
                {
                    affectedThings.Add(thing);
                }
            }

            bool isFirstTarget = true;
            foreach (Thing thing in affectedThings)
            {
                ApplyDamageToTarget(thing, isFirstTarget);
                isFirstTarget = false;
            }

            if (affectedThings.Count == 0 && Props.impactEffecter != null && !effecterPlayed)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(center, map), new TargetInfo(center, map));
                effecter.Cleanup();
                effecterPlayed = true;
            }
        }
    }
}