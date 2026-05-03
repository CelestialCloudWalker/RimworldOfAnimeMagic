using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityFanProjectile : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;
        public int projectileCount = 3;
        public float spreadAngle = 25f;

        public CompProperties_AbilityFanProjectile()
        {
            compClass = typeof(CompAbilityEffect_FanProjectile);
        }
    }

    public class CompAbilityEffect_FanProjectile : CompAbilityEffect
    {
        public new CompProperties_AbilityFanProjectile Props =>
            (CompProperties_AbilityFanProjectile)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null || Props.projectileDef == null) return;

            Vector3 origin = caster.DrawPos;
            Vector3 targetPos = target.Cell.ToVector3Shifted();
            Vector3 baseDir = (targetPos - origin);
            if (baseDir == Vector3.zero) baseDir = Vector3.forward;
            baseDir.y = 0f;

            float baseAngle = Mathf.Atan2(baseDir.x, baseDir.z) * Mathf.Rad2Deg;
            float distToTarget = baseDir.magnitude;
            int count = Props.projectileCount;
            float halfSpread = Props.spreadAngle / 2f;
            float step = count > 1 ? Props.spreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1 ? -halfSpread + step * i : 0f;
                float angle = baseAngle + offset;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 shotDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                IntVec3 destination = (origin + shotDir * distToTarget).ToIntVec3();
                if (!destination.InBounds(caster.Map))
                    destination = target.Cell;

                Projectile proj = (Projectile)GenSpawn.Spawn(
                    Props.projectileDef, caster.Position, caster.Map);

                if (proj is ScalingStatDamageProjectile scalingProj)
                    scalingProj.SetDamageScale(1f);

                proj.Launch(
                    caster,
                    origin,
                    new LocalTargetInfo(destination), 
                    new LocalTargetInfo(destination),
                    ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetPawns,
                    false,
                    null);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent?.pawn != null && target.IsValid && Props.projectileDef != null;
        }
    }
}