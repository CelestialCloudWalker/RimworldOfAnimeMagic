using System;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_EquipCompRemoveHediffOnHit : CompProperties
    {
        public bool ApplyToSelf = false;
        public bool ApplyOnTarget = true;
        public float Chance = 0.5f;
        public float Severity = 1f;
        public List<HediffDef> hediffsToRemove;

        public CompProperties_EquipCompRemoveHediffOnHit()
        {
            compClass = typeof(EquipComp_RemoveHediffOnHit);
        }
    }

    public class EquipComp_RemoveHediffOnHit : ThingCompExt
    {
        public CompProperties_EquipCompRemoveHediffOnHit Props => (CompProperties_EquipCompRemoveHediffOnHit)props;

        public override DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            if (Rand.Range(0, 1) > Props.Chance)
                return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);

            Pawn targetPawn = Props.ApplyOnTarget ? target.Pawn : null;
            Pawn selfPawn = Props.ApplyToSelf ? _EquipOwner : null;
            Pawn pawn = targetPawn ?? selfPawn;

            if (pawn?.health?.hediffSet != null)
            {
                for (int i = 0; i < Props.hediffsToRemove.Count; i++)
                {
                    var def = Props.hediffsToRemove[i];
                    var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                    if (hediff != null)
                        pawn.health.RemoveHediff(hediff);
                }
            }

            return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
        }
    }
}