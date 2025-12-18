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

            IntVec3 targetCell = target.Cell;
            IntVec3 startPos = caster.Position;

            if (Props.castEffecter != null)
            {
                var castEffect = Props.castEffecter.Spawn();
                castEffect.Trigger(new TargetInfo(startPos, caster.Map), new TargetInfo(startPos, caster.Map));
                castEffect.Cleanup();
            }

            if (Props.castSound != null)
                Props.castSound.PlayOneShot(new TargetInfo(startPos, caster.Map));

            IntVec3 dashTarget = FindBestRetreatPosition(caster, targetCell);

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

                if (Props.landSound != null)
                    Props.landSound.PlayOneShot(new TargetInfo(dashTarget, caster.Map));
            }
        }

        private IntVec3 FindBestRetreatPosition(Pawn caster, IntVec3 targetCell)
        {
            Map map = caster.Map;
            float maxRange = parent.def.verbProperties.range;

            if (IsValidRetreatCell(targetCell, caster, map, maxRange))
                return targetCell;

            for (int radius = 1; radius <= 3; radius++)
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(targetCell, radius, true))
                {
                    if (IsValidRetreatCell(cell, caster, map, maxRange))
                        return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsValidRetreatCell(IntVec3 cell, Pawn caster, Map map, float maxRange)
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
                return false;

            if (cell.GetEdifice(map)?.def.passability == Traversability.Impassable)
                return false;

            if (caster.Position.DistanceTo(cell) > maxRange)
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

            IntVec3 retreatPos = FindBestRetreatPosition(caster, target.Cell);
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
        public bool avoidEnemies = true;
        public float enemyAvoidanceRadius = 5f;

        public EffecterDef castEffecter;
        public EffecterDef landEffecter;
        public SoundDef castSound;
        public SoundDef landSound;

        public CompProperties_AbilityRetreatDash()
        {
            compClass = typeof(CompAbilityEffect_RetreatDash);
        }
    }
}