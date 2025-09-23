using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_RetreatDash : CompAbilityEffect
    {
        public new CompProperties_AbilityRetreatDash Props => (CompProperties_AbilityRetreatDash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null) return;

            IntVec3 originalTarget = target.Cell;

            for (int dashCount = 0; dashCount < Props.numberOfDashes; dashCount++)
            {
                IntVec3 currentPos = caster.Position;

                if (dashCount == 0 && Props.castEffecter != null)
                {
                    var castEffect = Props.castEffecter.Spawn();
                    castEffect.Trigger(new TargetInfo(currentPos, caster.Map), new TargetInfo(currentPos, caster.Map));
                    castEffect.Cleanup();
                }
                else if (dashCount > 0 && Props.retryEffecter != null)
                {
                    var retryEffect = Props.retryEffecter.Spawn();
                    retryEffect.Trigger(new TargetInfo(currentPos, caster.Map), new TargetInfo(currentPos, caster.Map));
                    retryEffect.Cleanup();
                }

                IntVec3 dashTarget = CalculateRetreatPosition(caster, originalTarget);

                if (dashTarget.IsValid)
                {
                    caster.Position = dashTarget;
                    caster.Notify_Teleported(false, false);

                    if (Props.landEffecter != null)
                    {
                        var landEffect = Props.landEffecter.Spawn();
                        landEffect.Trigger(new TargetInfo(dashTarget, caster.Map), new TargetInfo(dashTarget, caster.Map));
                        landEffect.Cleanup();
                    }

                    if (dashCount == 0 && Props.castSound != null)
                        Props.castSound.PlayOneShot(new TargetInfo(currentPos, caster.Map));

                    if (Props.landSound != null)
                        Props.landSound.PlayOneShot(new TargetInfo(dashTarget, caster.Map));
                }
                else
                {
                    break;
                }
            }
        }

        private IntVec3 CalculateRetreatPosition(Pawn caster, IntVec3 fromCell)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;

            Vector3 retreatDirection = (casterPos - fromCell).ToVector3Shifted().normalized;

            for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
            {
                Vector3 targetVector = casterPos.ToVector3Shifted() + (retreatDirection * dist);
                IntVec3 candidate = targetVector.ToIntVec3();

                if (IsValidRetreatCell(candidate, caster, map))
                    return candidate;

                for (int angle = -45; angle <= 45; angle += 15)
                {
                    Vector3 rotatedDir = retreatDirection.RotatedBy(angle);
                    Vector3 rotatedVector = casterPos.ToVector3Shifted() + (rotatedDir * dist);
                    IntVec3 rotatedCandidate = rotatedVector.ToIntVec3();

                    if (IsValidRetreatCell(rotatedCandidate, caster, map))
                        return rotatedCandidate;
                }
            }

            // fallback to random positions
            for (int i = 0; i < 20; i++)
            {
                float randomAngle = Rand.Range(0f, 360f);
                Vector3 randomDir = Vector3.forward.RotatedBy(randomAngle);

                for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
                {
                    Vector3 randomVector = casterPos.ToVector3Shifted() + (randomDir * dist);
                    IntVec3 randomCandidate = randomVector.ToIntVec3();

                    if (IsValidRetreatCell(randomCandidate, caster, map))
                        return randomCandidate;
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsValidRetreatCell(IntVec3 cell, Pawn caster, Map map)
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
                return false;

            if (cell.GetEdifice(map)?.def.passability == Traversability.Impassable)
                return false;

            if (Props.avoidEnemies)
            {
                var enemies = map.mapPawns.AllPawnsSpawned.Where(p =>
                    p.HostileTo(caster) && p.Position.DistanceTo(cell) < Props.enemyAvoidanceRadius);
                if (enemies.Any())
                    return false;
            }

            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;

            Pawn caster = parent.pawn;
            if (caster == null)
                return false;

            IntVec3 retreatPos = CalculateRetreatPosition(caster, target.Cell);
            if (!retreatPos.IsValid)
            {
                if (throwMessages)
                    Messages.Message("No valid retreat position found.", MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }
    }

    public class CompProperties_AbilityRetreatDash : CompProperties_AbilityEffect
    {
        public int minDashDistance = 3;
        public int maxDashDistance = 8;
        public bool avoidEnemies = true;
        public float enemyAvoidanceRadius = 5f;
        public int numberOfDashes = 1;

        public EffecterDef castEffecter;
        public EffecterDef landEffecter;
        public EffecterDef retryEffecter;
        public SoundDef castSound;
        public SoundDef landSound;

        public CompProperties_AbilityRetreatDash()
        {
            compClass = typeof(CompAbilityEffect_RetreatDash);
        }
    }
}