using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace AnimeArsenal
{
    public class SunlightDamageExtension : DefModExtension
    {
        public float damagePerTick = 0.1f;
        public float damageThresholdBeforeDeath = 10f;
        public int ticksBetweenDamage = 250;
    }

    public class MapComponent_SunlightDamage : MapComponent
    {
        private Dictionary<int, float> accumulatedDamage = new Dictionary<int, float>();
        private Effecter burningEffecter;
        private Effecter deathEffecter;

        public MapComponent_SunlightDamage(Map map) : base(map)
        {
            burningEffecter = CelestialDefof.SunlightBurningEffect.Spawn();
            deathEffecter = CelestialDefof.SunlightDeathEffect.Spawn();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref accumulatedDamage, "accumulatedDamage", LookMode.Value, LookMode.Value);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            var extension = DefDatabase<GeneDef>.GetNamed("BloodDemonArt")
                .GetModExtension<SunlightDamageExtension>();
            if (extension == null) return;

            if (Find.TickManager.TicksGame % extension.ticksBetweenDamage != 0) return;

            List<Pawn> pawnsToProcess = map.mapPawns.AllPawnsSpawned
                .Where(p => HasBloodDemonArt(p) && IsExposedToSunlight(p))
                .ToList();

            foreach (Pawn pawn in pawnsToProcess)
            {
                if (pawn?.Destroyed != true)
                {
                    DealSunlightDamage(pawn);
                }
            }

            List<int> pawnsToRemove = accumulatedDamage.Keys
                .Where(id => !map.mapPawns.AllPawnsSpawned.Any(p => p.thingIDNumber == id))
                .ToList();

            foreach (int id in pawnsToRemove)
            {
                accumulatedDamage.Remove(id);
            }
        }

        private bool HasBloodDemonArt(Pawn pawn)
        {
            return pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) ?? false;
        }

        private bool IsExposedToSunlight(Pawn pawn)
        {
            return map.skyManager.CurSkyGlow > 0.5f && !pawn.Position.Roofed(map);
        }

        private void DealSunlightDamage(Pawn pawn)
        {
            var extension = DefDatabase<GeneDef>.GetNamed("BloodDemonArt")
                .GetModExtension<SunlightDamageExtension>();
            if (extension == null) return;

            DamageInfo damageInfo = new DamageInfo(
                DamageDefOf.Burn,
                extension.damagePerTick,
                0f,
                -1f,
                null,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown,
                null,
                true,
                true,
                QualityCategory.Normal,
                true,
                false
            );

            pawn.TakeDamage(damageInfo);

            if (burningEffecter != null)
            {
                burningEffecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
            }
            MoteMaker.ThrowText(pawn.DrawPos, map, "Burning in sunlight!", 3f);

            if (!accumulatedDamage.ContainsKey(pawn.thingIDNumber))
            {
                accumulatedDamage[pawn.thingIDNumber] = 0f;
            }
            accumulatedDamage[pawn.thingIDNumber] += extension.damagePerTick;

            if (accumulatedDamage[pawn.thingIDNumber] >= extension.damageThresholdBeforeDeath)
            {
                KillPawn(pawn);
            }
        }

        private void KillPawn(Pawn pawn)
        {
            if (deathEffecter != null)
            {
                deathEffecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
            }

            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 3);

            pawn.Kill(null);

            accumulatedDamage.Remove(pawn.thingIDNumber);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (burningEffecter == null)
            {
                burningEffecter = CelestialDefof.SunlightBurningEffect.Spawn();
            }
            if (deathEffecter == null)
            {
                deathEffecter = CelestialDefof.SunlightDeathEffect.Spawn();
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            if (burningEffecter != null)
            {
                burningEffecter.Cleanup();
                burningEffecter = null;
            }
            if (deathEffecter != null)
            {
                deathEffecter.Cleanup();
                deathEffecter = null;
            }
        }
    }
}