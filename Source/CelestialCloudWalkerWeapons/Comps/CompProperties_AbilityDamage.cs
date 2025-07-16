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
                if (!string.IsNullOrEmpty(Props.damageDef))
                {
                    cachedDamageDef = DefDatabase<DamageDef>.GetNamed(Props.damageDef, false);

                   
                    if (cachedDamageDef == null)
                    {
                        Log.Error($"AnimeArsenal: Could not find DamageDef named '{Props.damageDef}'. Using Cut damage instead.");
                        cachedDamageDef = DamageDefOf.Cut;
                    }
                }
                else
                {
                    cachedDamageDef = DamageDefOf.Cut;
                }
            }

            return cachedDamageDef;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!target.HasThing)
                return;
            Thing targetThing = target.Thing;
            Map map = targetThing?.Map;

            if (targetThing == null || map == null || parent?.pawn == null)
                return;
            
            int finalDamage = Props.damageAmount;
            if (Props.meleeSkillFactor > 0)
            {
                int meleeSkill = parent.pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                int skillBonus = (int)(meleeSkill * Props.meleeSkillFactor);
                finalDamage += skillBonus;

                
                Log.Message($"AbilityDamage - Base damage: {Props.damageAmount}, Melee skill: {meleeSkill}, Bonus: {skillBonus}, Final: {finalDamage}");
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
            if (target == null) return;

            DamageInfo dinfo = new DamageInfo(
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

            Log.Message($"AbilityDamage - Applying {amount} {GetDamageDef().label} damage to {target.Label}");

            target.TakeDamage(dinfo);

            if (Props.stunTicks > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances?.stunner?.StunFor(Props.stunTicks, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map, int amount)
        {
            if (map == null) return;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, map, Props.radius, true))
            {
                if (thing != parent.pawn)
                {
                    ApplyDamage(thing, amount);
                }
            }
        }
    }
}