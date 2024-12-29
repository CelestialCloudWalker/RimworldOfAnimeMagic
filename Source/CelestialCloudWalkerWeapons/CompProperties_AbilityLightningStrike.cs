using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityLightningStrike : CompProperties_AbilityEffect
    {
        public bool lightning = true;
        public float explosionRadius = 3f;
        public int explosionDamage = 50;
        public SoundDef soundOnImpact;

        public CompProperties_AbilityLightningStrike()
        {
            compClass = typeof(CompAbilityEffect_LightningStrike);
        }
    }

    public class CompAbilityEffect_LightningStrike : CompAbilityEffect
    {
        public new CompProperties_AbilityLightningStrike Props => (CompProperties_AbilityLightningStrike)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Map map = parent.pawn.Map;
            IntVec3 strikeLocation = target.Cell;

            // Create and fire the lightning strike event
            WeatherEvent_LightningStrike lightningStrike = new WeatherEvent_LightningStrike(map, strikeLocation);
            lightningStrike.FireEvent();

            // Apply additional effects
            if (Props.explosionRadius > 0f)
            {
                GenExplosion.DoExplosion(
                    strikeLocation,
                    map,
                    Props.explosionRadius,
                    DamageDefOf.Bomb,
                    parent.pawn,
                    Props.explosionDamage,
                    -1f
                );
            }
        }

        private int lastUsedTick = -999999;
        private const int aiDecisionCooldown = 300;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Find.TickManager.TicksGame < lastUsedTick + aiDecisionCooldown)
            {
                return false;
            }
            lastUsedTick = Find.TickManager.TicksGame;
            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return base.Valid(target, throwMessages) && target.Cell.Standable(parent.pawn.Map);
        }
    }
}