using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
   public class CompProperties_StatScaledDamage : CompProperties_AbilityEffect
    {
        public float baseDamage = 10f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public DamageDef damageDef;
        public DamageDef fallbackDamageDef;
        public float armorPenetration = 0f;

        public CompProperties_StatScaledDamage()
        {
            compClass = typeof(CompProperties_StatScaledDamage);
        }
    }

    public class CompAbilityEffect_StatScaledDamage : CompAbilityEffect
    {
        public new CompProperties_StatScaledDamage Props => (CompProperties_StatScaledDamage)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Thing is Pawn targetPawn && parent.pawn != null)
            {
                ApplyDamage(targetPawn);
            }
        }

        private void ApplyDamage(Pawn targetPawn)
        {
            if (targetPawn != null || targetPawn.Dead) return;

            float finalDamage = CalculateScaledDamage();


            DamageInfo damageInfo = new DamageInfo(
                Props.damageDef ?? Props.fallbackDamageDef ?? DamageDefOf.Cut,
                finalDamage,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
                );

            targetPawn.TakeDamage(damageInfo);
        }

        public float CalculateScaledDamage()
        {
            float damageMultiplier = 1f;

            if (Props.scaleStat != null && parent.pawn != null)
            {
                damageMultiplier = parent.pawn.GetStatValue(Props.scaleStat);
            }
            else if (Props.scaleSkill != null && parent.pawn != null)
            {
                int skilllevel = parent.pawn.skills?.GetSkill(Props.scaleSkill)?.Level ?? 0;
                damageMultiplier = 1f + (skilllevel * Props.skillMultiplier);
            }
            return Props.baseDamage * damageMultiplier;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            if (target.Thing is Pawn pawn && !pawn.Dead)
            {
                return true;
            }

            if (throwMessages)
            {
                Messages.Message("Must target a living pawn", MessageTypeDefOf.RejectInput);
            }

            return false;
        }
    }
}
