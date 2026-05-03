using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_StatScaledDamage : CompProperties_AbilityEffect
    {
        public float baseDamage = 10f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public DamageDef damageDef;
        public DamageDef fallbackDamageDef;
        public float armorPen = 0f;
        public float downedThreshold = 0f;
        public int repeatAmount = 0;
        public int damageInterval = 0;
        public int knockbackDistance = 0;
        public bool knockbackRandom = false;
        public EffecterDef impactEffecter;
        public FleckDef impactFleck;
        public EffecterDef castEffecter;
        public FleckDef castFleck;

        public CompProperties_StatScaledDamage()
        {
            compClass = typeof(CompAbilityEffect_StatScaledDamage);
        }
    }

    public class CompAbilityEffect_StatScaledDamage : CompAbilityEffect
    {
        public new CompProperties_StatScaledDamage Props => (CompProperties_StatScaledDamage)props;

        private int hitsRemaining = 0;
        private int ticksUntilNextHit = 0;
        private Pawn savedTarget;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!(target.Thing is Pawn targetPawn) || parent.pawn == null) return;

            SpawnEffecter(Props.castEffecter, parent.pawn.Position, parent.pawn.Map);
            SpawnFleck(Props.castFleck, parent.pawn.Map, parent.pawn.DrawPos);

            if (Props.repeatAmount <= 0)
            {
                ApplyDamage(targetPawn);
                return;
            }

            savedTarget = targetPawn;
            hitsRemaining = Props.repeatAmount;
            ticksUntilNextHit = 0;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (hitsRemaining <= 0) return;

            ticksUntilNextHit--;
            if (ticksUntilNextHit > 0) return;

            Pawn caster = parent.pawn;
            if (caster == null || caster.Destroyed || savedTarget == null || savedTarget.Dead || savedTarget.Destroyed)
            {
                hitsRemaining = 0;
                return;
            }

            ApplyDamage(savedTarget);

            hitsRemaining--;
            ticksUntilNextHit = Props.damageInterval;
        }

        private void ApplyDamage(Pawn targetPawn)
        {
            if (targetPawn == null || targetPawn.Dead) return;

            if (Props.downedThreshold > 0f && !targetPawn.Downed)
            {
                float healthFraction = targetPawn.health.summaryHealth.SummaryHealthPercent;
                if (healthFraction < Props.downedThreshold)
                {
                    targetPawn.health.forceDowned = true;
                }
            }

            float finalDamage = CalculateScaledDamage();
            DamageInfo damageInfo = new DamageInfo(
                Props.damageDef ?? Props.fallbackDamageDef ?? DamageDefOf.Cut,
                finalDamage,
                Props.armorPen,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );
            targetPawn.TakeDamage(damageInfo);

            if (targetPawn != null && !targetPawn.Dead)
            {
                targetPawn.health.forceDowned = false;
            }

            SpawnEffecter(Props.impactEffecter, targetPawn.Position, targetPawn.Map);
            SpawnFleck(Props.impactFleck, targetPawn.Map, targetPawn.DrawPos);

            if (Props.knockbackDistance > 0)
            {
                ApplyKnockback(targetPawn);
            }
        }

        private void ApplyKnockback(Pawn targetPawn)
        {
            if (targetPawn == null || targetPawn.Dead || targetPawn.Downed) return;

            if (Props.knockbackRandom)
            {
                ApplyKnockbackRandom(targetPawn);
            }
            else
            {
                ApplyKnockbackDirectional(targetPawn);
            }
        }

        private void ApplyKnockbackDirectional(Pawn targetPawn)
        {
            IntVec3 current = targetPawn.Position;
            IntVec3 direction = targetPawn.Position - parent.pawn.Position;
            direction.x = System.Math.Sign(direction.x);
            direction.z = System.Math.Sign(direction.z);

            for (int step = 0; step < Props.knockbackDistance; step++)
            {
                IntVec3 next = current + direction;
                if (!next.InBounds(targetPawn.Map) || !next.Walkable(targetPawn.Map)) break;
                current = next;
            }

            if (current != targetPawn.Position)
            {
                targetPawn.Position = current;
                targetPawn.Notify_Teleported(false, true);
            }
        }

        private void ApplyKnockbackRandom(Pawn targetPawn)
        {
            IntVec3 origin = targetPawn.Position;
            IntVec3 current = origin;
            IntVec3 direction = origin.RandomAdjacentCell8Way() - origin;

            for (int step = 0; step < Props.knockbackDistance; step++)
            {
                IntVec3 next = current + direction;
                if (!next.InBounds(targetPawn.Map) || !next.Walkable(targetPawn.Map)) break;
                current = next;
            }

            if (current != origin)
            {
                targetPawn.Position = current;
                targetPawn.Notify_Teleported(false, true);
            }
        }

        private void SpawnEffecter(EffecterDef def, IntVec3 position, Map map)
        {
            if (def == null || map == null) return;
            Effecter effecter = def.Spawn(position, map);
            effecter.Cleanup();
        }

        private void SpawnFleck(FleckDef def, Map map, UnityEngine.Vector3 pos)
        {
            if (def == null || map == null) return;
            FleckMaker.Static(pos, map, def);
        }

        public float CalculateScaledDamage()
        {
            float damageMultiplier = 1f;
            if (Props.scaleStat != null && parent.pawn != null)
            {
                damageMultiplier = parent.pawn.GetStatValue(Props.scaleStat);
            }
            else if (Props.scaleSkill != null && parent.pawn != null)
            {
                int skillLevel = parent.pawn.skills?.GetSkill(Props.scaleSkill)?.Level ?? 0;
                damageMultiplier = 1f + (skillLevel * Props.skillMultiplier);
            }
            return Props.baseDamage * damageMultiplier;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;
            if (target.Thing is Pawn pawn && !pawn.Dead)
            {
                return true;
            }
            if (throwMessages)
            {
                Messages.Message("Must target a living pawn", MessageTypeDefOf.RejectInput);
            }
            return false;
        }
    }
}