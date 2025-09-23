using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_EquipCompScaleableDamage : CompProperties
    {
        public float baseIncrease = 0.2f;
        public float highSkillIncrease = 0.35f;
        public int highSkillThreshold = 18;

        public CompProperties_EquipCompScaleableDamage()
        {
            compClass = typeof(EquipComp_ScaleableDamage);
        }
    }

    public class EquipComp_ScaleableDamage : BaseTraitComp
    {
        public override string TraitName => "Scaling:Melee";
        public override string Description => "This equipment's damage will hit harder the better the pawns melee skill.";

        private CompProperties_EquipCompScaleableDamage Props => (CompProperties_EquipCompScaleableDamage)props;

        public override DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            var result = base.Notify_ApplyMeleeDamageToTarget(target, DamageWorkerResult);

            if (EquipOwner?.skills == null) return result;

            int skill = EquipOwner.skills.GetSkill(SkillDefOf.Melee).Level;
            float multiplier = skill >= Props.highSkillThreshold ? Props.highSkillIncrease : Props.baseIncrease;

            result.totalDamageDealt *= (1f + multiplier);

            return result;
        }
    }
}