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

        public CompProperties_DamageInRect()
        {
            compClass = typeof(CompAbilityEffect_DamageInRect);
        }
    }

    public class CompAbilityEffect_DamageInRect : CompAbilityEffect
    {
        public new CompProperties_DamageInRect Props => (CompProperties_DamageInRect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = parent.pawn;
            Vector3 origin = GetOrigin(pawn, target);
            float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
            List<Thing> targets = GetTargetsInRect(pawn.Map, origin, angle);
            ApplyDamageToTargets(pawn, targets);
        }

        protected virtual Vector3 GetOrigin(Pawn pawn, LocalTargetInfo target)
        {
            return Props.useMouseAsOrigin ? target.CenterVector3 : pawn.DrawPos;
        }

        protected virtual float GetAngleToTarget(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        protected virtual List<Thing> GetTargetsInRect(Map map, Vector3 origin, float angle)
        {
            return RotatedRectTargetFinder.GetTargetsInRotatedRect(map, origin, Props.rectWidth, Props.rectLength, angle);
        }

        protected virtual void ApplyDamageToTargets(Pawn caster, List<Thing> targets)
        {
            foreach (Thing thing in targets)
            {
                if (thing != caster && !thing.Destroyed)
                {
                    ApplyDamageToTarget(caster, thing);
                }
            }
        }

        protected virtual void ApplyDamageToTarget(Pawn caster, Thing target)
        {
            DamageInfo dinfo = new DamageInfo(Props.damageType, Props.damageAmount, 0, -1, caster);
            target.TakeDamage(dinfo);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            Vector3 origin = GetOrigin(pawn, target);
            float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
            List<IntVec3> cells = RotatedRectTargetFinder.GetCellsInRotatedRect(pawn.Map, origin, Props.rectWidth, Props.rectLength, angle);
            GenDraw.DrawFieldEdges(cells, Color.red);
        }
    }
    public class CompProperties_BeamDamageInRect : CompProperties_DamageInRect
    {
        public ThingDef beamMoteDef;
        public EffecterDef beamEndMoteDef;
        public int beamDurationTicks = 60;
        public float beamWidth = 10f;

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
            CreatePowerBeam(parent.pawn.Position, target.Cell, parent.pawn.Map);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (powerBeam != null && currentTarget.IsValid)
            {
                powerBeam.TickBeam(currentTarget.Cell);
                if (!powerBeam.IsRunning)
                {
                    powerBeam = null;
                }
            }
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {

            return true;
        }

        private void CreatePowerBeam(IntVec3 source, IntVec3 target, Map map)
        {
            if (Props.beamMoteDef == null) return;
            powerBeam = new PowerBeamVisual(Props.beamMoteDef, Props.beamEndMoteDef, source, map, Props.beamWidth, Props.beamDurationTicks);
            powerBeam.InitializeBeam(source, target);
            powerBeam.TickBeam(target);
        }

        protected override void ApplyDamageToTarget(Pawn caster, Thing target)
        {
            base.ApplyDamageToTarget(caster, target);
            // Add any additional beam-specific effects here
        }
    }
    //public class CompProperties_DamageInRect : CompProperties_AbilityEffect
    //{
    //    public float rectWidth = 10f;
    //    public float rectLength = 2f;
    //    public DamageDef damageType;
    //    public int damageAmount = 10;
    //    public bool useMouseAsOrigin = false;
    //    public ThingDef beamMoteDef;
    //    public EffecterDef beamEndMoteDef;
    //    public int beamDurationTicks = 60;
    //    public float beamWidth = 10f;

    //    public CompProperties_DamageInRect()
    //    {
    //        compClass = typeof(CompAbilityEffect_DamageInRect);
    //    }
    //}

    //public class CompAbilityEffect_DamageInRect : CompAbilityEffect
    //{
    //    public new CompProperties_DamageInRect Props => (CompProperties_DamageInRect)props;
    //    private PowerBeamVisual powerBeam;
    //    private LocalTargetInfo currentTarget;

    //    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    //    {
    //        Pawn pawn = parent.pawn;
    //        currentTarget = target;
    //        Vector3 origin = Props.useMouseAsOrigin ? target.CenterVector3 : pawn.DrawPos;
    //        float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
    //        List<Thing> targets = RotatedRectTargetFinder.GetTargetsInRotatedRect(pawn.Map, origin, Props.rectWidth, Props.rectLength, angle);

    //        foreach (Thing thing in targets)
    //        {
    //            if (thing != pawn && !thing.Destroyed)
    //            {
    //                DamageInfo dinfo = new DamageInfo(Props.damageType, Props.damageAmount, 0, -1, pawn);
    //                thing.TakeDamage(dinfo);
    //            }
    //        }

    //        CreatePowerBeam(pawn.Position, target.Cell, pawn.Map);

    //    }

    //    public override void CompTick()
    //    {
    //        base.CompTick();
    //        if (powerBeam != null && currentTarget.IsValid)
    //        {
    //            powerBeam.TickBeam(currentTarget.Cell);
    //            if (!powerBeam.IsRunning)
    //            {
    //                powerBeam = null;
    //            }
    //        }
    //    }

    //    private void CreatePowerBeam(IntVec3 source, IntVec3 target, Map map)
    //    {
    //        if (Props.beamMoteDef == null) return;

    //        powerBeam = new PowerBeamVisual(Props.beamMoteDef, Props.beamEndMoteDef, source, map, Props.beamWidth, Props.beamDurationTicks);
    //        powerBeam.InitializeBeam(source, target);
    //        powerBeam.TickBeam(target);
    //    }

    //    public override void DrawEffectPreview(LocalTargetInfo target)
    //    {
    //        Pawn pawn = parent.pawn;
    //        Vector3 origin = Props.useMouseAsOrigin ? target.CenterVector3 : pawn.DrawPos;
    //        float angle = GetAngleToTarget(origin, target.Cell.ToVector3());
    //        List<IntVec3> cells = RotatedRectTargetFinder.GetCellsInRotatedRect(pawn.Map, origin, Props.rectWidth, Props.rectLength, angle);
    //        GenDraw.DrawFieldEdges(cells, Color.red);
    //    }

    //    private float GetAngleToTarget(Vector3 origin, Vector3 target)
    //    {
    //        Vector3 direction = target - origin;
    //        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    //        return angle;
    //    }
    //}
}