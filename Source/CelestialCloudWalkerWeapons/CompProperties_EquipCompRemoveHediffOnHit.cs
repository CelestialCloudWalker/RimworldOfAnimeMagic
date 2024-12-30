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
            compClass = typeof(EquipComp_ApplyHediffOnHit);
        }
    }



    public class EquipComp_RemoveHediffOnHit : ThingCompExt
    {
        public CompProperties_EquipCompRemoveHediffOnHit Props => (CompProperties_EquipCompRemoveHediffOnHit)props;

        public override DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            if (Props.ApplyOnTarget && target.Pawn != null)
            {
                if (Rand.Range(0, 1) <= Props.Chance)
                {
                    foreach (var item in Props.hediffsToRemove)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        Hediff hediff = target.Pawn.health.hediffSet.GetFirstHediffOfDef(item);
                        if (hediff != null)
                        {
                            target.Pawn.health.RemoveHediff(hediff);
                        }
                    }
                    return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
                }
            }
            else if (Props.ApplyToSelf && _EquipOwner != null)
            {
                if (Rand.Range(0, 1) <= Props.Chance)
                {
                    foreach (var item in Props.hediffsToRemove)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        Hediff hediff = _EquipOwner.health.hediffSet.GetFirstHediffOfDef(item);
                        if (hediff != null)
                        {
                            _EquipOwner.health.RemoveHediff(hediff);
                        }
                    }

                    return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
                }
            }
            return base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
        }
    }
}