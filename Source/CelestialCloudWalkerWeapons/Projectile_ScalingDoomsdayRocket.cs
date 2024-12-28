using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class Projectile_ScalingDoomsdayRocket : ScalingStatDamageProjectile
    {
        public override bool AnimalsFleeImpact => true;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            base.Impact(hitThing, blockedByShield);

            float explosionRadius = this.def.projectile.explosionRadius;

            GenExplosion.DoExplosion(
                center: base.Position,
                map: map,
                radius: explosionRadius,
                damType: this.def.projectile.damageDef,
                instigator: this.launcher,
                damAmount: this.DamageAmount,
                armorPenetration: this.ArmorPenetration,
                explosionSound: null,
                weapon: this.equipmentDef,
                projectile: this.def,
                intendedTarget: this.intendedTarget.Thing,
                postExplosionSpawnThingDef: ThingDefOf.Filth_Fuel,
                postExplosionSpawnChance: 0.2f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0.4f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                doVisualEffects: true,
                propagationSpeed: 1f,
                excludeRadius: 0f,
                doSoundEffects: true,
                postExplosionGasType: null,
                screenShakeFactor: 1f
            );

            CellRect cellRect = CellRect.CenteredOn(base.Position, 5);
            cellRect.ClipInsideMap(map);
            for (int i = 0; i < 3; i++)
            {
                IntVec3 randomCell = cellRect.RandomCell;
                DoFireExplosion(randomCell, map, 3.9f);
            }
        }

        protected void DoFireExplosion(IntVec3 pos, Map map, float radius)
        {
            GenExplosion.DoExplosion(
                center: pos,
                map: map,
                radius: radius,
                damType: DamageDefOf.Flame,
                instigator: this.launcher,
                damAmount: this.DamageAmount,
                armorPenetration: this.ArmorPenetration,
                explosionSound: null,
                weapon: this.equipmentDef,
                projectile: this.def,
                intendedTarget: this.intendedTarget.Thing,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: 0f,
                damageFalloff: false,
                direction: null,
                ignoredThings: null,
                doVisualEffects: true,
                propagationSpeed: 1f,
                excludeRadius: 0f,
                doSoundEffects: true,
                postExplosionGasType: null,
                screenShakeFactor: 1f
            );
        }

        private const int ExtraExplosionCount = 3;
        private const int ExtraExplosionRadius = 5;
    }
}