using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class SunlightDamageExtension : DefModExtension
    {
        public float damagePerTick = 0.1f;
        public float damageThresholdBeforeDeath = 10f;
        public int ticksBetweenDamage = 250;
        public int ticksToResetDamage = 2500;
        public float minimumCoverageForProtection = 0.95f;
        public bool requireHeadCoverage = true;
        public float sunTolerancePool = 15f;
    }

    public class MapComponent_SunlightDamage : MapComponent
    {
        private Dictionary<int, float> accumulatedDamage = new Dictionary<int, float>();
        private Dictionary<int, int> ticksOutOfSun = new Dictionary<int, int>();
        private Dictionary<int, int> lastWarningTick = new Dictionary<int, int>();
        private Dictionary<int, float> sunTolerance = new Dictionary<int, float>();
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
            Scribe_Collections.Look(ref ticksOutOfSun, "ticksOutOfSun", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastWarningTick, "lastWarningTick", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref sunTolerance, "sunTolerance", LookMode.Value, LookMode.Value);

            // Initialize dictionaries if they're null after loading
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (accumulatedDamage == null) accumulatedDamage = new Dictionary<int, float>();
                if (ticksOutOfSun == null) ticksOutOfSun = new Dictionary<int, int>();
                if (lastWarningTick == null) lastWarningTick = new Dictionary<int, int>();
                if (sunTolerance == null) sunTolerance = new Dictionary<int, float>();
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Safety check - ensure dictionaries are initialized
            if (sunTolerance == null) sunTolerance = new Dictionary<int, float>();
            if (accumulatedDamage == null) accumulatedDamage = new Dictionary<int, float>();
            if (ticksOutOfSun == null) ticksOutOfSun = new Dictionary<int, int>();
            if (lastWarningTick == null) lastWarningTick = new Dictionary<int, int>();

            var vulnPawns = map.mapPawns.AllPawnsSpawned.Where(p => HasSunlightVuln(p)).ToList();

            foreach (var pawn in vulnPawns)
            {
                if (pawn?.Destroyed != true)
                {
                    var ext = GetSunlightExt(pawn);
                    if (ext != null) ProcessPawn(pawn, ext);
                }
            }

            var toRemove = accumulatedDamage.Keys.Where(id => !map.mapPawns.AllPawnsSpawned.Any(p => p.thingIDNumber == id)).ToList();
            foreach (int id in toRemove)
            {
                accumulatedDamage.Remove(id);
                ticksOutOfSun.Remove(id);
                lastWarningTick.Remove(id);
                sunTolerance.Remove(id);
            }
        }

        private void ProcessPawn(Pawn pawn, SunlightDamageExtension ext)
        {
            bool exposed = IsExposed(pawn, ext);
            int id = pawn.thingIDNumber;

            if (exposed)
            {
                ticksOutOfSun.Remove(id);
                if (Find.TickManager.TicksGame % ext.ticksBetweenDamage == 0)
                {
                    // Check if tolerance is depleted before applying damage
                    float currentTolerance = GetSunTolerance(pawn, ext);

                    if (currentTolerance <= 0)
                    {
                        // Tolerance depleted - now apply actual damage
                        DoDamage(pawn);
                    }
                    else
                    {
                        // Still have tolerance - just deplete it
                        DepleteTolerance(pawn, ext);
                    }
                }
            }
            else
            {
                if (!ticksOutOfSun.ContainsKey(id)) ticksOutOfSun[id] = 0;
                ticksOutOfSun[id]++;

                if (ticksOutOfSun[id] >= ext.ticksToResetDamage)
                {
                    if (accumulatedDamage.ContainsKey(id))
                    {
                        accumulatedDamage.Remove(id);
                        lastWarningTick.Remove(id);
                        MoteMaker.ThrowText(pawn.DrawPos, map, "Sunlight damage reset", 2f);
                    }
                    if (sunTolerance.ContainsKey(id))
                    {
                        sunTolerance.Remove(id);
                        MoteMaker.ThrowText(pawn.DrawPos, map, "Sun tolerance restored", 2f);
                    }
                    ticksOutOfSun.Remove(id);
                }
            }
        }

        private float GetSunTolerance(Pawn pawn, SunlightDamageExtension ext)
        {
            if (sunTolerance == null) sunTolerance = new Dictionary<int, float>();

            int id = pawn.thingIDNumber;
            if (!sunTolerance.ContainsKey(id))
            {
                // Initialize tolerance - use sunTolerancePool if available, otherwise fall back to threshold
                float initialTolerance = ext.sunTolerancePool > 0 ? ext.sunTolerancePool : ext.damageThresholdBeforeDeath;
                sunTolerance[id] = initialTolerance;

                Log.Message($"[SunlightDamage] Initialized tolerance for {pawn.LabelShort}: {initialTolerance}");
            }
            return sunTolerance[id];
        }

        private void DepleteTolerance(Pawn pawn, SunlightDamageExtension ext)
        {
            if (sunTolerance == null) sunTolerance = new Dictionary<int, float>();

            int id = pawn.thingIDNumber;
            if (!sunTolerance.ContainsKey(id))
            {
                float initialTolerance = ext.sunTolerancePool > 0 ? ext.sunTolerancePool : ext.damageThresholdBeforeDeath;
                sunTolerance[id] = initialTolerance;
            }

            sunTolerance[id] -= ext.damagePerTick;

            float maxTolerance = ext.sunTolerancePool > 0 ? ext.sunTolerancePool : ext.damageThresholdBeforeDeath;
            float tolerancePercent = (sunTolerance[id] / maxTolerance) * 100f;

            if (Find.TickManager.TicksGame % 500 == 0)
            {
                MoteMaker.ThrowText(pawn.DrawPos, map, $"Sun tolerance: {tolerancePercent:F1}%", 2f);
            }

            // Warning when tolerance is getting low
            if (sunTolerance[id] <= maxTolerance * 0.2f && sunTolerance[id] > 0)
            {
                if (!lastWarningTick.ContainsKey(id) || Find.TickManager.TicksGame - lastWarningTick[id] >= 1000)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, "WARNING: Sun tolerance depleting!", 3f);
                    lastWarningTick[id] = Find.TickManager.TicksGame;
                }
            }
        }

        private bool HasSunlightVuln(Pawn pawn)
        {
            if (pawn.genes == null) return false;
            return pawn.genes.GenesListForReading.Any(gene => gene.Active && gene.def.GetModExtension<SunlightDamageExtension>() != null);
        }

        private SunlightDamageExtension GetSunlightExt(Pawn pawn)
        {
            if (pawn.genes == null) return null;
            var gene = pawn.genes.GenesListForReading.FirstOrDefault(g => g.Active && g.def.GetModExtension<SunlightDamageExtension>() != null);
            return gene?.def.GetModExtension<SunlightDamageExtension>();
        }

        private bool IsExposed(Pawn pawn, SunlightDamageExtension ext)
        {
            if (map.skyManager.CurSkyGlow <= 0.5f || pawn.Position.Roofed(map)) return false;
            if (ext.requireHeadCoverage && !HasHeadCover(pawn)) return true;

            float coverage = CalcCoverage(pawn);
            return coverage < ext.minimumCoverageForProtection;
        }

        private bool HasHeadCover(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null || pawn.apparel.WornApparel.Count == 0) return false;

            var headParts = new List<string> { "FullHead", "UpperHead", "Eyes" };

            foreach (string partName in headParts)
            {
                var bodyPart = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(partName);
                if (bodyPart != null)
                {
                    foreach (Apparel app in pawn.apparel.WornApparel)
                    {
                        if (app.def.apparel.bodyPartGroups.Contains(bodyPart)) return true;
                    }
                }
            }
            return false;
        }

        private float CalcCoverage(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null || pawn.apparel.WornApparel.Count == 0) return 0f;

            var criticalParts = new List<string>
            { "Torso", "Neck", "Shoulders", "Arms", "Legs", "FullHead", "UpperHead", "Eyes", "Face", "Hands", "Feet" };

            var bodyParts = new List<BodyPartGroupDef>();
            foreach (string partName in criticalParts)
            {
                var bodyPart = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(partName);
                if (bodyPart != null) bodyParts.Add(bodyPart);
            }

            if (bodyParts.Count == 0) return pawn.apparel.WornApparel.Any(a => a.def.IsApparel) ? 0.8f : 0f;

            float totalCoverage = 0f;
            int coveredParts = 0;

            foreach (var bodyPartGroup in bodyParts)
            {
                float partCoverage = 0f;
                foreach (var apparel in pawn.apparel.WornApparel)
                {
                    if (apparel.def.apparel.bodyPartGroups.Contains(bodyPartGroup))
                    {
                        float apparelCov = GetApparelCov(apparel);
                        partCoverage = Mathf.Max(partCoverage, apparelCov);
                    }
                }

                if (partCoverage > 0f)
                {
                    coveredParts++;
                    totalCoverage += partCoverage;
                }
            }

            if (bodyParts.Count == 0) return 0f;

            float bodyPartsCoveredRatio = (float)coveredParts / bodyParts.Count;
            float avgPartCoverage = coveredParts > 0 ? totalCoverage / coveredParts : 0f;
            return bodyPartsCoveredRatio * avgPartCoverage;
        }

        private float GetApparelCov(Apparel apparel)
        {
            var layer = apparel.def.apparel.LastLayer;

            if (layer == ApparelLayerDefOf.Shell || layer == ApparelLayerDefOf.Middle) return 1.0f;
            else if (layer == ApparelLayerDefOf.OnSkin) return 0.6f;

            if (apparel.def.apparel.bodyPartGroups.Count >= 2) return 0.8f;
            return 0.5f;
        }

        private void DoDamage(Pawn pawn)
        {
            var ext = GetSunlightExt(pawn);
            if (ext == null) return;

            if (burningEffecter != null)
            {
                burningEffecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
            }

            if (!accumulatedDamage.ContainsKey(pawn.thingIDNumber)) accumulatedDamage[pawn.thingIDNumber] = 0f;
            accumulatedDamage[pawn.thingIDNumber] += ext.damagePerTick;

            int tick = Find.TickManager.TicksGame;
            float damagePercent = (accumulatedDamage[pawn.thingIDNumber] / ext.damageThresholdBeforeDeath) * 100f;

            if (tick % 1000 == 0)
            {
                float coverage = CalcCoverage(pawn);
                bool hasHeadCover = HasHeadCover(pawn);
                string headStatus = hasHeadCover ? "Protected" : "EXPOSED";
                MoteMaker.ThrowText(pawn.DrawPos, map, $"Head: {headStatus} | Coverage: {coverage:P0} | Exposure: {damagePercent:F1}%", 2f);
            }
            else if (tick % 500 == 0)
            {
                if (ext.requireHeadCoverage && !HasHeadCover(pawn))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, $"Head exposed! Damage: {damagePercent:F1}%", 2f);
                }
                else
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, $"Sunlight exposure: {damagePercent:F1}%", 2f);
                }
            }

            if (accumulatedDamage[pawn.thingIDNumber] >= ext.damageThresholdBeforeDeath * 0.8f)
            {
                if (!lastWarningTick.ContainsKey(pawn.thingIDNumber) || tick - lastWarningTick[pawn.thingIDNumber] >= 1000)
                {
                    string warning = ext.requireHeadCoverage && !HasHeadCover(pawn)
                        ? "WARNING: Head exposed to sunlight!"
                        : "WARNING: High sunlight exposure!";
                    MoteMaker.ThrowText(pawn.DrawPos, map, warning, 3f);
                    lastWarningTick[pawn.thingIDNumber] = tick;
                }
            }

            if (accumulatedDamage[pawn.thingIDNumber] >= ext.damageThresholdBeforeDeath) KillPawn(pawn);
        }

        public float GetSunlightDamageLevel(Pawn pawn)
        {
            if (accumulatedDamage == null || !accumulatedDamage.ContainsKey(pawn.thingIDNumber)) return 0f;
            return accumulatedDamage[pawn.thingIDNumber];
        }

        public float GetSunlightDamagePercentage(Pawn pawn)
        {
            var ext = GetSunlightExt(pawn);
            if (ext == null) return 0f;
            float currentDamage = GetSunlightDamageLevel(pawn);
            return (currentDamage / ext.damageThresholdBeforeDeath) * 100f;
        }

        public float GetSunTolerancePercentage(Pawn pawn)
        {
            var ext = GetSunlightExt(pawn);
            if (ext == null) return 100f;

            float currentTolerance = GetSunTolerance(pawn, ext);
            float maxTolerance = ext.sunTolerancePool > 0 ? ext.sunTolerancePool : ext.damageThresholdBeforeDeath;

            return (currentTolerance / maxTolerance) * 100f;
        }

        public float GetArmorCoverage(Pawn pawn) => CalcCoverage(pawn);
        public bool GetHeadCoverage(Pawn pawn) => HasHeadCover(pawn);

        private void KillPawn(Pawn pawn)
        {
            if (deathEffecter != null)
            {
                deathEffecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
            }

            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 3);
            pawn.Kill(null);

            accumulatedDamage.Remove(pawn.thingIDNumber);
            ticksOutOfSun.Remove(pawn.thingIDNumber);
            lastWarningTick.Remove(pawn.thingIDNumber);
            sunTolerance.Remove(pawn.thingIDNumber);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Ensure all dictionaries are initialized
            if (accumulatedDamage == null) accumulatedDamage = new Dictionary<int, float>();
            if (ticksOutOfSun == null) ticksOutOfSun = new Dictionary<int, int>();
            if (lastWarningTick == null) lastWarningTick = new Dictionary<int, int>();
            if (sunTolerance == null) sunTolerance = new Dictionary<int, float>();

            if (burningEffecter == null) burningEffecter = CelestialDefof.SunlightBurningEffect.Spawn();
            if (deathEffecter == null) deathEffecter = CelestialDefof.SunlightDeathEffect.Spawn();
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