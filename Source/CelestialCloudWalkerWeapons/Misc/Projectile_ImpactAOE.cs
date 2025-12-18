using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class Projectile_ImpactAOE : ScalingStatDamageProjectile
    {
        public ProjectileProperties_ImpactAOE Props => (ProjectileProperties_ImpactAOE)def.projectile;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var targets = AnimeArsenalUtility.GetThingsInRange(Position, MapHeld, Props.ExplosionRadius, IsValidTarget).ToList();

            AnimeArsenalUtility.DealDamageToThingsInRange(
                targets,
                Props.damageDef,
                Props.aoeDamage,
                Props.GetArmorPenetration(launcher)
            );

            foreach (var target in targets.OfType<Pawn>().Where(p => !p.Destroyed))
            {
                TryPushBack(target);
            }

            Props.ExplosionEffect?.Spawn(Position, MapHeld);

            base.Impact(hitThing, blockedByShield);
        }

        private bool IsValidTarget(Thing target)
        {
            if (target == this || (launcher != null && target == launcher && !Props.CanHitCaster))
                return false;
            if (target.Faction != null && !target.Faction.HostileTo(launcher.Faction) && !Props.CanHitFriendly)
                return false;
            return true;
        }

        private void TryPushBack(Pawn target)
        {
            if (launcher?.Map == null) return;

            var direction = target.Position - launcher.Position;
            var knockback = target.Position + direction * Rand.Range(1, 4);
            knockback = knockback.ClampInsideMap(launcher.Map);

            if (GenAdj.TryFindRandomAdjacentCell8WayWithRoom(target, out var destination))
            {
                var map = launcher.Map;
                if (destination.IsValid && destination.InBounds(map) && destination.Walkable(map))
                {
                    var flyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_Flyer, target, destination, null, null);
                    GenSpawn.Spawn(flyer, target.Position, map);
                }
            }
        }
    }

    public class ProjectileProperties_ImpactAOE : ProjectileProperties
    {
        public float ExplosionRadius = 10f;
        public EffecterDef ExplosionEffect;
        public bool CanHitFriendly = true;
        public bool CanHitCaster = false;
        public float aoeDamage = 7f; 
    }
}