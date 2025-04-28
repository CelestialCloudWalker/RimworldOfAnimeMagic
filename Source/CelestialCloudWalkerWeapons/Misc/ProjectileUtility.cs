using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public static class ProjectileUtility
    {
        public static void ReflectProjectile(Projectile projectile, Thing Reflector)
        {
            if (projectile.Launcher.Faction != Faction.OfPlayer)
            {
                Thing originalLauncher = projectile.Launcher;
                IntVec3 position = projectile.Position;
                ThingDef projectileDef = projectile.def;
                projectile.Destroy();
                Projectile newProjectile = (Projectile)GenSpawn.Spawn(projectileDef, position, Reflector.MapHeld);
                newProjectile.Launch(Reflector, Reflector.DrawPos, originalLauncher, originalLauncher, ProjectileHitFlags.IntendedTarget);
                EffecterDefOf.Deflect_General_Bullet.Spawn(position, Reflector.MapHeld);
            }
        }

        public static void RedirectProjectile(Thing Launcher, Projectile projectile, IntVec3 TargetPosition)
        {
            if (projectile.Launcher.Faction != Faction.OfPlayer)
            {
                Thing originalLauncher = projectile.Launcher;
                IntVec3 position = projectile.Position;
                ThingDef projectileDef = projectile.def;
                projectile.Destroy();
                Projectile newProjectile = (Projectile)GenSpawn.Spawn(projectileDef, position, Launcher.MapHeld);
                newProjectile.Launch(Launcher, TargetPosition, TargetPosition, ProjectileHitFlags.IntendedTarget);
                EffecterDefOf.Deflect_General_Bullet.Spawn(position, Launcher.MapHeld);
            }
        }

        public static void DeflectProjectile(Projectile projectile, Pawn reflector)
        {
            if (projectile.Launcher.Faction != Faction.OfPlayer)
            {
                Thing originalLauncher = projectile.Launcher;
                IntVec3 position = projectile.Position;
                ThingDef projectileDef = projectile.def;
                projectile.Destroy();
                Projectile newProjectile = (Projectile)GenSpawn.Spawn(projectileDef, position, reflector.MapHeld);
                newProjectile.Launch(reflector, reflector.DrawPos, position + new IntVec3(Random.Range(-1, 1), 0, Random.Range(-1, 1)), originalLauncher, ProjectileHitFlags.None);
                EffecterDefOf.Deflect_General_Bullet.Spawn(position, reflector.MapHeld);
            }
        }
    }
}