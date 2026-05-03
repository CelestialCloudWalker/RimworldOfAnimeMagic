using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DamageInRect : CompProperties_AbilityEffect
    {
        public float rectWidth = 10f;
        public float rectLength = 2f;
        public DamageDef damageType;
        public int damageAmount = 10;
        public bool useMouseAsOrigin = false;
        public bool useOriginAsCenter = false;
        public int repeatAmount = 0;
        public int damageInterval = 0;
        public int delayTicks = 0;
        public float stunChance = 0f;
        public int stunDuration = 0;
        public int knockbackDistance = 0;
        public bool knockbackDirectional = false;
        public bool knockbackRandom = false;
        public EffecterDef impactEffecter;
        public FleckDef impactFleck;
        public EffecterDef castEffecter;
        public FleckDef castFleck;

        public HediffDef hediffDef;
        public float hediffSeverity = 0f;

        public CompProperties_DamageInRect()
        {
            compClass = typeof(CompAbilityEffect_DamageInRect);
        }
    }

    public class CompAbilityEffect_DamageInRect : CompAbilityEffect
    {
        public new CompProperties_DamageInRect Props => (CompProperties_DamageInRect)props;

        private int hitsRemaining = 0;
        private int ticksUntilNextHit = 0;
        private LocalTargetInfo savedTarget;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!target.IsValid || target.Cell == IntVec3.Invalid)
                target = new LocalTargetInfo(parent.pawn.Position);

            savedTarget = target;

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Map == null) return;

            if (Props.delayTicks > 0)
            {
                hitsRemaining = Props.repeatAmount > 0 ? Props.repeatAmount : 1;
                ticksUntilNextHit = Props.delayTicks;
                return;
            }

            SpawnEffecter(Props.castEffecter, pawn.Position, pawn.Map);
            SpawnFleck(Props.castFleck, pawn.Map, pawn.DrawPos);

            if (Props.repeatAmount <= 0)
            {
                Vector3 origin = GetOrigin(pawn, target);
                float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
                List<Thing> targets = GetTargetsInRect(pawn.Map, origin, angle);
                ApplyDamageToTargets(pawn, targets);
                return;
            }

            hitsRemaining = Props.repeatAmount;
            ticksUntilNextHit = 0;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (hitsRemaining <= 0) return;

            ticksUntilNextHit--;
            if (ticksUntilNextHit > 0) return;

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Destroyed || pawn.Map == null)
            {
                hitsRemaining = 0;
                return;
            }

            if (hitsRemaining == (Props.repeatAmount > 0 ? Props.repeatAmount : 1))
            {
                SpawnEffecter(Props.castEffecter, pawn.Position, pawn.Map);
                SpawnFleck(Props.castFleck, pawn.Map, pawn.DrawPos);
            }

            Vector3 origin = GetOrigin(pawn, savedTarget);
            float angle = GetAngleToTarget(origin, savedTarget.Cell.ToVector3());
            List<Thing> targets = GetTargetsInRect(pawn.Map, origin, angle);
            ApplyDamageToTargets(pawn, targets);

            hitsRemaining--;
            ticksUntilNextHit = Props.damageInterval;
        }

        protected virtual Vector3 GetOrigin(Pawn pawn, LocalTargetInfo target)
        {
            if (Props.useMouseAsOrigin) return target.CenterVector3;
            return pawn.DrawPos;
        }

        protected virtual float GetAngleToTarget(Vector3 origin, Vector3 target)
        {
            if (Props.useOriginAsCenter) return 0f;
            Vector3 direction = target - origin;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        protected virtual List<Thing> GetTargetsInRect(Map map, Vector3 origin, float angle)
        {
            return RotatedRectTargetFinder.GetTargetsInRotatedRect(
                map, origin, Props.rectWidth, Props.rectLength, angle);
        }

        protected virtual void ApplyDamageToTargets(Pawn caster, List<Thing> targets)
        {
            foreach (Thing thing in targets)
            {
                if (thing != caster && !thing.Destroyed)
                    ApplyDamageToTarget(caster, thing);
            }
        }

        protected virtual void ApplyDamageToTarget(Pawn caster, Thing target)
        {
            DamageInfo dinfo = new DamageInfo(Props.damageType, Props.damageAmount, 0, -1, caster);

            try
            {
                target.TakeDamage(dinfo);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[AnimeArsenal] DamageInRect: TakeDamage threw on " +
                            $"{target?.LabelShort ?? "null"} — this is likely a " +
                            $"HAR/EMF interaction bug, not an AnimeArsenal bug. " +
                            $"Details: {ex.Message}");
            }

            SpawnEffecter(Props.impactEffecter, target.Position, target.Map);
            SpawnFleck(Props.impactFleck, target.Map, target.DrawPos);

            if (Props.stunChance > 0f && target is Pawn targetPawn && !targetPawn.Dead)
            {
                if (Rand.Chance(Props.stunChance))
                    targetPawn.stances.stunner.StunFor(Props.stunDuration, caster);
            }

            if (Props.knockbackDistance > 0 && target is Pawn knockPawn && !knockPawn.Dead)
                ApplyKnockback(knockPawn, caster);

            if (Props.hediffDef != null && Props.hediffSeverity > 0f
                && target is Pawn hediffPawn && !hediffPawn.Dead)
            {
                Hediff existing = hediffPawn.health.hediffSet
                    .GetFirstHediffOfDef(Props.hediffDef);

                if (existing != null)
                {
                    existing.Severity += Props.hediffSeverity;
                }
                else
                {
                    Hediff newHediff = HediffMaker.MakeHediff(Props.hediffDef, hediffPawn);
                    newHediff.Severity = Props.hediffSeverity;
                    hediffPawn.health.AddHediff(newHediff);
                }
            }
        }

        private void ApplyKnockback(Pawn targetPawn, Pawn caster)
        {
            if (targetPawn == null || targetPawn.Dead || targetPawn.Downed) return;
            if (Props.knockbackDirectional) ApplyKnockbackDirectional(targetPawn, caster);
            if (Props.knockbackRandom) ApplyKnockbackRandom(targetPawn);
        }

        private void ApplyKnockbackDirectional(Pawn targetPawn, Pawn caster)
        {
            IntVec3 current = targetPawn.Position;
            IntVec3 direction = targetPawn.Position - caster.Position;
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

        protected void SpawnEffecter(EffecterDef def, IntVec3 position, Map map)
        {
            if (def == null || map == null) return;
            Effecter e = def.Spawn(position, map);
            e.EffectTick(new TargetInfo(position, map), TargetInfo.Invalid);
            e.EffectTick(new TargetInfo(position, map), TargetInfo.Invalid);
            e.EffectTick(new TargetInfo(position, map), TargetInfo.Invalid);
            e.Cleanup();
        }

        protected void SpawnFleck(FleckDef def, Map map, Vector3 pos)
        {
            if (def == null || map == null) return;
            FleckMaker.Static(pos, map, def);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Map == null) return;
            Vector3 origin = GetOrigin(pawn, target);
            float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
            List<IntVec3> cells = RotatedRectTargetFinder.GetCellsInRotatedRect(
                pawn.Map, origin, Props.rectWidth, Props.rectLength, angle);
            GenDraw.DrawFieldEdges(cells, Color.red);
        }
    }

    public class CompProperties_BeamDamageInRect : CompProperties_DamageInRect
    {
        public ThingDef beamMoteDef;
        public EffecterDef beamEndMoteDef;
        public int beamDurationTicks = 60;
        public float beamWidth = 2f;

        public CompProperties_BeamDamageInRect()
        {
            compClass = typeof(CompAbilityEffect_BeamDamageInRect);
        }
    }

    public class CompAbilityEffect_BeamDamageInRect : CompAbilityEffect_DamageInRect
    {
        public new CompProperties_BeamDamageInRect Props => (CompProperties_BeamDamageInRect)props;

        private PowerBeamVisual powerBeam;
        private LocalTargetInfo currentTarget;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            currentTarget = target;

            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            if (Props.beamMoteDef != null)
            {
                powerBeam = new PowerBeamVisual(
                    Props.beamMoteDef,
                    Props.beamEndMoteDef,
                    pawn.Position,
                    pawn.Map,
                    Props.beamWidth,
                    Props.beamDurationTicks);

                powerBeam.InitializeBeam(pawn.Position, target.Cell);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (powerBeam != null && currentTarget.IsValid)
            {
                powerBeam.TickBeam(currentTarget.Cell);
                if (!powerBeam.IsRunning)
                    powerBeam = null;
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target) => true;
    }
}