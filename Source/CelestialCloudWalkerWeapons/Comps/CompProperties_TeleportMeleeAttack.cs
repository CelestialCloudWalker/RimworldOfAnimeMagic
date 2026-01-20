using RimWorld;
using Verse;
using System.Collections.Generic;
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

        public StatDef scaleStat;
        public SkillDef scaleSkill;  
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;

        public CompProperties_TeleportMeleeAttack()
        {
            compClass = typeof(CompAbilityEffect_TeleportMeleeAttack);
        }
    }

    public class CompAbilityEffect_TeleportMeleeAttack : CompAbilityEffect
    {
        private Thing target;
        private IntVec3 targetPosition;
        private Map targetMap;
        private bool impactPlayed = false;

        private new CompProperties_TeleportMeleeAttack Props => (CompProperties_TeleportMeleeAttack)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!target.HasThing || parent?.pawn == null) return;

            this.target = target.Thing;
            this.targetPosition = target.Thing.Position;
            this.targetMap = target.Thing.Map;
            impactPlayed = false;

            SpawnCasterEffect();
            StartTeleport();
        }

        private void SpawnCasterEffect()
        {
            if (Props.casterEffecter == null || parent?.pawn == null) return;

            var effecter = Props.casterEffecter.Spawn();
            var casterInfo = new TargetInfo(parent.pawn.Position, parent.pawn.Map);
            effecter.Trigger(casterInfo, casterInfo);
            effecter.Cleanup();
        }

        private void StartTeleport()
        {
            if (target?.Map == null)
            {
                Log.Warning("[Anime Arsenal] Cannot start teleport: invalid target or position");
                return;
            }

            var flyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_TPStrikeFlyer, parent.pawn, targetPosition, null, null);
            var spawnedFlyer = GenSpawn.Spawn(flyer, targetPosition, targetMap) as DelegateFlyer;

            if (spawnedFlyer != null)
                spawnedFlyer.OnRespawnPawn += OnTeleportComplete;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.HasThing;
        }

        private void OnTeleportComplete(Pawn pawn, PawnFlyer flyer, Map map)
        {
            try
            {
                if (pawn == null || pawn.Destroyed || map == null || parent?.pawn == null)
                {
                    Log.Warning("[Anime Arsenal] OnTeleportComplete: Invalid pawn or map state");
                    return;
                }

                if (!pawn.Spawned || pawn.Map != map)
                {
                    Log.Warning("[Anime Arsenal] OnTeleportComplete: Pawn not properly spawned on map");
                    return;
                }

                if (target != null && !target.Destroyed && target.Spawned && target.Map == map)
                {
                    targetPosition = target.Position;
                    targetMap = target.Map;
                }

                PositionPawn(map);
                ApplyDamage();
            }
            finally
            {
                if (flyer is DelegateFlyer df)
                    df.OnRespawnPawn -= OnTeleportComplete;
            }
        }

        private void PositionPawn(Map map)
        {
            if (parent?.pawn == null || !parent.pawn.Spawned) return;

            var adjacentCell = targetPosition.RandomAdjacentCell8Way();
            if (adjacentCell.InBounds(map) && adjacentCell.Standable(map))
            {
                parent.pawn.Position = adjacentCell;
                parent.pawn.stances?.SetStance(new Stance_Mobile());
            }
        }

        private void ApplyDamage()
        {
            if (target != null && !target.Destroyed && target.Spawned)
            {
                targetPosition = target.Position;
                targetMap = target.Map;

                if (Props.effectRadius <= 0f)
                {
                    DamageTarget(target);
                }
                else
                {
                    DamageArea();
                }
            }
            else
            {
                PlayImpactEffect(targetPosition);
            }
        }

        private void DamageTarget(Thing target)
        {
            if (target == null || target.Destroyed || parent?.pawn == null) return;

            var damage = CalculateDamage();
            var damageType = Props.damageType ?? DamageDefOf.Cut;

            var dinfo = new DamageInfo(damageType, damage, Props.armorPenetration, -1f, parent.pawn, null, null,
                DamageInfo.SourceCategory.ThingOrUnknown, target);

            target.TakeDamage(dinfo);
            PlayImpactEffect(target.Position);
            ApplyStun(target);
        }

        private void DamageArea()
        {
            if (targetMap == null)
            {
                Log.Warning("[Anime Arsenal] DamageArea: Invalid map or position");
                return;
            }

            var targets = new List<Thing>();

            foreach (var thing in GenRadial.RadialDistinctThingsAround(targetPosition, targetMap, Props.effectRadius, true))
            {
                if (thing != null && !thing.Destroyed && thing != parent.pawn && (thing is Pawn || thing.def.useHitPoints))
                    targets.Add(thing);
            }

            foreach (var thing in targets)
                DamageTarget(thing);

            if (targets.Count == 0)
                PlayImpactEffect(targetPosition);
        }

        private int CalculateDamage()
        {
            float baseDamage = Props.damageAmount;

            float scaledDamage = DamageScalingUtility.GetScaledDamage(
                baseDamage,
                parent.pawn,
                Props.scaleStat,
                Props.scaleSkill,
                Props.skillMultiplier,
                Props.debugScaling
            );

            if (Props.additionalDamageFactorFromMeleeSkill > 0 && parent.pawn != null)
            {
                var meleeLevel = parent.pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                scaledDamage += meleeLevel * Props.additionalDamageFactorFromMeleeSkill * Props.damageAmount;
            }

            return (int)scaledDamage;
        }

        private void PlayImpactEffect(IntVec3 position)
        {
            if (Props.impactEffecter == null || impactPlayed || targetMap == null) return;

            var effecter = Props.impactEffecter.Spawn();
            var targetInfo = new TargetInfo(position, targetMap);
            effecter.Trigger(targetInfo, targetInfo);
            effecter.Cleanup();
            impactPlayed = true;
        }

        private void ApplyStun(Thing target)
        {
            if (Props.stunDuration > 0 && target is Pawn pawn && parent?.pawn != null)
                pawn.stances?.stunner?.StunFor(Props.stunDuration, parent.pawn);
        }
    }
}