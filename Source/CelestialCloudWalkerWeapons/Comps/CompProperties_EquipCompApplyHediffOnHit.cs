using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_EquipCompApplyHediffOnHit : CompProperties
    {
        public bool ApplyToSelf = false;
        public bool ApplyOnTarget = true;
        public float ApplyChance = 0.5f;
        public float Severity = 1f;
        public HediffDef hediffToApply;

        public CompProperties_EquipCompApplyHediffOnHit()
        {
            compClass = typeof(EquipComp_ApplyHediffOnHit);
        }
    }

    public class EquipComp_ApplyHediffOnHit : BaseTraitComp
    {
        public CompProperties_EquipCompApplyHediffOnHit Props => (CompProperties_EquipCompApplyHediffOnHit)props;

        public override string TraitName => $"Apply {Props.hediffToApply.label}";

        public override string Description => $"This equipment has a chance({Props.ApplyChance * 100}) to apply {Props.hediffToApply.label} on a melee attack.";

        public override DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            if (Rand.Chance(Props.ApplyChance))
            {
                Pawn targetPawn = null;

                if (Props.ApplyOnTarget && target.Pawn != null)
                    targetPawn = target.Pawn;
                else if (Props.ApplyToSelf && _EquipOwner != null)
                    targetPawn = _EquipOwner;

                if (targetPawn != null)
                {
                    var hediff = targetPawn.health.GetOrAddHediff(Props.hediffToApply);
                    hediff.Severity = Props.Severity;
                }
            }

            return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
        }
    }
}