using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDamage : AbilityCompProperties
    {
        public int damageAmount = 15;
        public string damageDef = "Cut";
        public float armorPenetration = 0.3f;
        public int stunTicks = 0;
        public bool applyToTarget = true;
        public float meleeSkillFactor = 0.0f;
        public float radius = 0f;

        public CompProperties_AbilityDamage()
        {
            compClass = typeof(CompAbilityEffect_Damage);
        }
    }

    public class CompAbilityEffect_Damage : CompAbilityEffect
    {
        public new CompProperties_AbilityDamage Props => (CompProperties_AbilityDamage)props;

        private DamageDef cachedDamageDef = null;

        private DamageDef GetDamageDef()
        {
            if (cachedDamageDef == null)
            {
                cachedDamageDef = DefDatabase<DamageDef>.GetNamedSilentFail(Props.damageDef) ?? DamageDefOf.Cut;
            }
            return cachedDamageDef;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!target.HasThing) return;

            var targetThing = target.Thing;
            var map = targetThing.Map;

            var finalDamage = Props.damageAmount;
            if (Props.meleeSkillFactor > 0)
            {
                var meleeSkill = parent.pawn.skills.GetSkill(SkillDefOf.Melee).Level;
                finalDamage += (int)(meleeSkill * Props.meleeSkillFactor);
            }

            if (Props.radius > 0f)
            {
                ApplyAreaDamage(targetThing.Position, map, finalDamage);
            }
            else if (Props.applyToTarget)
            {
                ApplyDamage(targetThing, finalDamage);
            }

            FleckMaker.Static(targetThing.Position, map, FleckDefOf.ExplosionFlash, 12f);
            FleckMaker.ThrowMicroSparks(targetThing.DrawPos, map);
        }

        private void ApplyDamage(Thing target, int amount)
        {
            var dinfo = new DamageInfo(
                GetDamageDef(),
                amount,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown,
                target
            );

            target.TakeDamage(dinfo);

            if (Props.stunTicks > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances.stunner.StunFor(Props.stunTicks, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map, int amount)
        {
            foreach (var thing in GenRadial.RadialDistinctThingsAround(center, map, Props.radius, true))
            {
                if (thing != parent.pawn)
                {
                    ApplyDamage(thing, amount);
                }
            }
        }
    }
}