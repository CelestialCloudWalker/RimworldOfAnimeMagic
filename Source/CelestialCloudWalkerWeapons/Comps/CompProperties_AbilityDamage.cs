using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDamage : CompProperties_AbilityEffect
    {
        public int damageAmount = 15;
        public string damageDef = "Cut";
        public float armorPenetration = 0.3f;
        public int stunTicks = 0;
        public bool applyToTarget = true;
        public float meleeSkillFactor = 0.0f;
        public float radius = 0f;
        public EffecterDef casterEffecter;
        public EffecterDef impactEffecter;

        public CompProperties_AbilityDamage()
        {
            compClass = typeof(CompAbilityEffect_Damage);
        }
    }

    public class CompAbilityEffect_Damage : CompAbilityEffect
    {
        public new CompProperties_AbilityDamage Props => props as CompProperties_AbilityDamage;

        private DamageDef GetDamageDef()
        {
            return DefDatabase<DamageDef>.GetNamedSilentFail(Props?.damageDef) ?? DamageDefOf.Cut;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (Props == null || !target.HasThing) return;

            Pawn instigator = parent?.pawn;
            if (instigator == null) return;

            Thing targetThing = target.Thing;
            Map map = targetThing.Map;
            if (map == null) return; 

            if (Props.casterEffecter != null)
            {
                Effecter e = Props.casterEffecter.Spawn();
                e.Trigger(new TargetInfo(instigator.Position, map), new TargetInfo(instigator.Position, map));
                e.Cleanup();
            }

            int finalDamage = Props.damageAmount;
            if (Props.meleeSkillFactor > 0f && instigator.skills != null)
            {
                int meleeSkill = instigator.skills.GetSkill(SkillDefOf.Melee).Level;
                finalDamage += (int)(meleeSkill * Props.meleeSkillFactor);
            }

            if (Props.radius > 0f)
                ApplyAreaDamage(targetThing.Position, map, finalDamage, instigator);
            else if (Props.applyToTarget)
                ApplyDamage(targetThing, finalDamage, instigator);

            if (Props.impactEffecter != null)
            {
                Effecter e = Props.impactEffecter.Spawn();
                e.Trigger(new TargetInfo(targetThing.Position, map), new TargetInfo(targetThing.Position, map));
                e.Cleanup();
            }
            else
            {
                FleckMaker.Static(targetThing.Position, map, FleckDefOf.ExplosionFlash, 12f);
                FleckMaker.ThrowMicroSparks(targetThing.DrawPos, map);
            }
        }

        private void ApplyDamage(Thing target, int amount, Pawn instigator)
        {
            var dinfo = new DamageInfo(
                GetDamageDef(), amount, Props.armorPenetration, -1f, instigator,
                null, null, DamageInfo.SourceCategory.ThingOrUnknown, target);
            target.TakeDamage(dinfo);

            if (Props.stunTicks > 0 && target is Pawn targetPawn)
                targetPawn.stances.stunner.StunFor(Props.stunTicks, instigator);
        }

        private void ApplyAreaDamage(IntVec3 center, Map map, int amount, Pawn instigator)
        {
            foreach (var thing in GenRadial.RadialDistinctThingsAround(center, map, Props.radius, true))
            {
                if (thing != instigator)
                    ApplyDamage(thing, amount, instigator);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return Props != null && target.HasThing && parent?.pawn != null;
        }
    }
}