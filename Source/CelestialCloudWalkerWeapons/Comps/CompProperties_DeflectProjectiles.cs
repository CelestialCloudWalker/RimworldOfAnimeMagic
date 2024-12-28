using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DeflectProjectiles : CompProperties_AbilityEffect
    {
        public float Radius = 5f;
        public int TicksActive = 1250;
        public int MaxShotsToReflect = 10;

        public CompProperties_DeflectProjectiles()
        {
            compClass = typeof(CompAbilityEffect_DeflectProjectiles);
        }
    }



    public class CompAbilityEffect_DeflectProjectiles : CompAbilityEffect
    {
        public new CompProperties_DeflectProjectiles Props => (CompProperties_DeflectProjectiles)props;
        private bool IsActive = false;
        private int TicksRemaining = 0;
        private int ShotsReflected = 0;

        private Effecter Effector;

        public override void CompTick()
        {
            base.CompTick();

            if (IsActive)
            {
                if (Effector != null)
                {
                    Effector.EffectTick(parent.pawn, parent.pawn);
                }

                if (TicksRemaining <= 0)
                {
                    DeactivateDeflection();
                }
                else
                {
                    TicksRemaining--;
                    DeflectProjectiles();
                }
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            ActivateDeflection();
        }

        private void ActivateDeflection()
        {
            IsActive = true;
            TicksRemaining = Props.TicksActive;
            ShotsReflected = 0;

            if (Effector != null)
            {
                Effector.Cleanup();
                Effector = null;
            }
            Effector = CelestialDefof.AnimeArsenal_Deflect.SpawnAttached(parent.pawn, parent.pawn.MapHeld);
        }

        private void DeactivateDeflection()
        {
            IsActive = false;
            TicksRemaining = 0;

            if (Effector != null)
            {
                Effector.Cleanup();
                Effector = null;
            }
        }

        public void DeflectProjectiles()
        {
            IEnumerable<Thing> projectilesInRange = GenRadial.RadialDistinctThingsAround(parent.pawn.Position, parent.pawn.MapHeld, Props.Radius, true);
            foreach (var item in projectilesInRange)
            {
                if (ShotsReflected > Props.MaxShotsToReflect)
                {
                    DeactivateDeflection();
                    break;
                }
                else
                {
                    if (item is Projectile projectile && projectile.Launcher.Faction != Faction.OfPlayer)
                    {
                        ProjectileUtility.ReflectProjectile(projectile, parent.pawn);
                        ShotsReflected++;
                    }
                }

            }
        }
    }
}
