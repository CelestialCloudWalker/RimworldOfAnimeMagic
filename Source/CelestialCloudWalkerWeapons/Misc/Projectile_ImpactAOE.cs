using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class Projectile_ImpactAOE : ScalingStatDamageProjectile
    {
        public  ProjectileProperties_ImpactAOE Props => (ProjectileProperties_ImpactAOE)this.def.projectile;

        

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
           
            List<Thing> ThingsToHit = AnimeArsenalUtility.GetThingsInRange(this.Position, this.MapHeld, this.Props.ExplosionRadius, TargetValidator).ToList();
            AnimeArsenalUtility.DealDamageToThingsInRange(ThingsToHit, Props.damageDef, Props.BaseDamage, Props.GetArmorPenetration(this.launcher));

            foreach (var thingToHit in ThingsToHit)
            {
                if (!thingToHit.Destroyed && thingToHit is Pawn pawnToHit)
                {
                    PushBack(pawnToHit);
                }
            }

            if (this.Props.ExplosionEffect != null)
            {
                this.Props.ExplosionEffect.Spawn(this.Position, this.MapHeld);
            }

            base.Impact(hitThing, blockedByShield);
        }

        private bool TargetValidator(Thing HitThing)
        {
            if (HitThing == this)
            {
                return false;
            }

            if (this.launcher != null && HitThing == this.launcher && !Props.CanHitCaster)
            {
                return false;
            }

            if (HitThing.Faction != null)
            {
                if (!HitThing.Faction.HostileTo(this.launcher.Faction) && !Props.CanHitFriendly)
                {
                    return false;
                }
            }

            return true;
        }

        private void PushBack(Pawn HitPawn)
        {
            if (launcher == null || launcher.Map == null)
            {
                return;
            }

            Map map = launcher.Map;

            IntVec3 launchDirection = HitPawn.Position - launcher.Position;

            IntVec3 destination = HitPawn.Position + launchDirection * Rand.Range(1, 4);
            destination = destination.ClampInsideMap(launcher.Map);

            IntVec3 finalDestinattion = IntVec3.Zero;

            if (GenAdj.TryFindRandomAdjacentCell8WayWithRoom(HitPawn, out finalDestinattion))
            {
                if (finalDestinattion.IsValid && finalDestinattion.InBounds(map) && finalDestinattion.Walkable(map))
                {
                    PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_Flyer, HitPawn, finalDestinattion, null, null);
                    GenSpawn.Spawn(pawnFlyer, HitPawn.Position, launcher.Map);
                }
            }
        }
    }

    public class ProjectileProperties_ImpactAOE : ProjectileProperties
    {
        public float BaseDamage = 1f;
        public float ExplosionRadius = 10f;
        public EffecterDef ExplosionEffect;
        public bool CanHitFriendly = true;
        public bool CanHitCaster = false;
    }
}
