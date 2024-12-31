using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_EquipCompScaleableDamage : CompProperties
    {
        public CompProperties_EquipCompScaleableDamage()
        {
            compClass = typeof(EquipComp_ScaleableDamage);
        }

        public float baseIncrease = 0.2f;
        public float highSkillIncrease = 0.35f;
        public int highSkillThreshold = 18;
    }

    public class EquipComp_ScaleableDamage : BaseTraitComp
    {
        public override string TraitName => "Scaling:Melee";

        public override string Description => "This equipment's damage will hit harder the better the pawns melee skill.";

        private CompProperties_EquipCompScaleableDamage Props => (CompProperties_EquipCompScaleableDamage)props;

        public override DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            DamageWorker.DamageResult damageResult = base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);
            if (EquipOwner != null)
            {
                int skillLevel = EquipOwner.skills.GetSkill(SkillDefOf.Melee).Level;
                float damageIncrease = CalculateDamageIncrease(skillLevel);

                float scaledDamage = damageResult.totalDamageDealt * (1f + damageIncrease);
                Log.Message($"Scaling damage {damageResult.totalDamageDealt} to {scaledDamage}");
                damageResult.totalDamageDealt = scaledDamage;
            }
            return damageResult;
        }

        private float CalculateDamageIncrease(int skillLevel)
        {
            if (skillLevel >= Props.highSkillThreshold)
            {
                return Props.highSkillIncrease;
            }
            else
            {
                return Props.baseIncrease;
            }
        }
    }
}