using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_TwistEffect : CompProperties_AbilityEffect
    {
        public float baseDamage = 15f; // Default damage value

        public CompProperties_TwistEffect()
        {
            compClass = typeof(TwistEffect);
        }
    }

    public class TwistEffect : CompAbilityEffect
    {
        public new CompProperties_TwistEffect Props => (CompProperties_TwistEffect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Thing is Pawn targetPawn)
            {
                TwistTargetLimb(targetPawn, parent.pawn, Props.baseDamage, 1f);
            }
        }

        public static void TwistTargetLimb(Pawn Target, Pawn Caster, float BaseDamage, float Scale)
        {
            BodyPartRecord targetLimb = AnimeArsenalUtility.GetRandomLimb(Target);
            if (targetLimb != null)
            {
                // Calculate damage
                float damage = BaseDamage * Scale;

                // Apply damage to the selected limb
                DamageInfo dinfo = new DamageInfo(CelestialDefof.TwistDamage, damage, 1f, -1f, Caster, targetLimb);
                Target.TakeDamage(dinfo);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.Thing is Pawn && base.Valid(target, throwMessages);
        }
    }
}