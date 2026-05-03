using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{

    public class HediffCompProperties_SeverityOnMeleeHitDealt : HediffCompProperties
    {
        public float severityGain = 0.04f;
        public float chanceToApply = 1f;

        public HediffCompProperties_SeverityOnMeleeHitDealt()
        {
            compClass = typeof(HediffComp_SeverityOnMeleeHitDealt);
        }
    }

    public class HediffComp_SeverityOnMeleeHitDealt : EMF.HediffComp_OnMeleeAttackEffect
    {
        HediffCompProperties_SeverityOnMeleeHitDealt Props =>
            (HediffCompProperties_SeverityOnMeleeHitDealt)props;

        protected override void OnMeleeAttack(
            Verb_MeleeAttackDamage meleeAttackVerb, LocalTargetInfo target)
        {
            base.OnMeleeAttack(meleeAttackVerb, target);

            if (target.Pawn == null || target.Pawn.Dead) return;
            if (!Rand.Chance(Props.chanceToApply)) return;

            parent.Severity = Mathf.Clamp01(parent.Severity + Props.severityGain);
        }
    }


    public class HediffCompProperties_SeverityOnMeleeHitReceived : HediffCompProperties
    {
        public float severityGain = 0.04f;
        public float chanceToApply = 1f;
        public bool meleeRangeOnly = true;

        public HediffCompProperties_SeverityOnMeleeHitReceived()
        {
            compClass = typeof(HediffComp_SeverityOnMeleeHitReceived);
        }
    }

    public class HediffComp_SeverityOnMeleeHitReceived : EMF.HediffComp_OnDamageTakenEffect
    {
        HediffCompProperties_SeverityOnMeleeHitReceived Props =>
            (HediffCompProperties_SeverityOnMeleeHitReceived)props;

        protected override void OnDamageTaken(DamageInfo dinfo)
        {
            if (dinfo.Instigator == null) return;
            if (!(dinfo.Instigator is Pawn attacker)) return;
            if (attacker == parent.pawn) return;
            if (!Rand.Chance(Props.chanceToApply)) return;

            if (Props.meleeRangeOnly &&
                !parent.pawn.Position.AdjacentTo8WayOrInside(attacker.Position))
                return;

            parent.Severity = Mathf.Clamp01(parent.Severity + Props.severityGain);
        }
    }
}