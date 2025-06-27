using RimWorld;
using Verse;
using System.Collections.Generic;
using System;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDamage : AbilityCompProperties
    {
        public int damageAmount = 15;

        // Changed from DamageDef with default to string that can be set in XML
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

        // Cache for damage def lookup
        private DamageDef cachedDamageDef = null;

        // Helper to get the proper DamageDef from the string
        private DamageDef GetDamageDef()
        {
            if (cachedDamageDef == null)
            {
                if (!string.IsNullOrEmpty(Props.damageDef))
                {
                    cachedDamageDef = DefDatabase<DamageDef>.GetNamed(Props.damageDef, false);

                    // Fallback to Cut if not found
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
            // Calculate final damage based on melee skill if configured
            int finalDamage = Props.damageAmount;
            if (Props.meleeSkillFactor > 0)
            {
                int meleeSkill = parent.pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                int skillBonus = (int)(meleeSkill * Props.meleeSkillFactor);
                finalDamage += skillBonus;

                // Debug logging
                Log.Message($"AbilityDamage - Base damage: {Props.damageAmount}, Melee skill: {meleeSkill}, Bonus: {skillBonus}, Final: {finalDamage}");
            }
            // Apply area damage if radius > 0
            if (Props.radius > 0f)
            {
                ApplyAreaDamage(targetThing.Position, map, finalDamage);
            }
            // Otherwise apply to single target
            else if (Props.applyToTarget)
            {
                ApplyDamage(targetThing, finalDamage);
            }

            // Visual effect
            FleckMaker.Static(targetThing.Position, map, FleckDefOf.ExplosionFlash, 12f);
            FleckMaker.ThrowMicroSparks(targetThing.DrawPos, map);
        }

        private void ApplyDamage(Thing target, int amount)
        {
            if (target == null) return;

            DamageInfo dinfo = new DamageInfo(
                GetDamageDef(),  // Using the helper method instead of Props.damageDef
                amount,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown,
                target
            );

            // Log for debugging
            Log.Message($"AbilityDamage - Applying {amount} {GetDamageDef().label} damage to {target.Label}");

            // Apply damage
            target.TakeDamage(dinfo);

            // Apply stun if configured and target is a pawn
            if (Props.stunTicks > 0 && target is Pawn targetPawn)
            {
                targetPawn.stances?.stunner?.StunFor(Props.stunTicks, parent.pawn);
            }
        }

        private void ApplyAreaDamage(IntVec3 center, Map map, int amount)
        {
            if (map == null) return;

            // Get all pawns in radius except self
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