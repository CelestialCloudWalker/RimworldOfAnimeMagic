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
    }

    public class MapComponent_SunlightDamage : MapComponent
    {
        private Dictionary<int, float> accumulatedDamage = new Dictionary<int, float>();
        private Dictionary<int, int> ticksOutOfSun = new Dictionary<int, int>();
        private Dictionary<int, int> lastWarningTick = new Dictionary<int, int>();
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
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            List<Pawn> pawnsWithSunlightVulnerability = map.mapPawns.AllPawnsSpawned
                .Where(p => HasSunlightVulnerability(p))
                .ToList();

            foreach (Pawn pawn in pawnsWithSunlightVulnerability)
            {
                if (pawn?.Destroyed != true)
                {
                    var extension = GetSunlightExtension(pawn);
                    if (extension != null)
                    {
                        ProcessPawnSunlightStatus(pawn, extension);
                    }
                }
            }

            List<int> pawnsToRemove = accumulatedDamage.Keys
                .Where(id => !map.mapPawns.AllPawnsSpawned.Any(p => p.thingIDNumber == id))
                .ToList();

            foreach (int id in pawnsToRemove)
            {
                accumulatedDamage.Remove(id);
                ticksOutOfSun.Remove(id);
                lastWarningTick.Remove(id);
            }
        }

        private void ProcessPawnSunlightStatus(Pawn pawn, SunlightDamageExtension extension)
        {
            bool isExposed = IsExposedToSunlight(pawn, extension);
            int pawnId = pawn.thingIDNumber;

            if (isExposed)
            {
                ticksOutOfSun.Remove(pawnId);

                if (Find.TickManager.TicksGame % extension.ticksBetweenDamage == 0)
                {
                    BuildSunlightMeter(pawn);
                }
            }
            else
            {
                if (!ticksOutOfSun.ContainsKey(pawnId))
                {
                    ticksOutOfSun[pawnId] = 0;
                }
                ticksOutOfSun[pawnId]++;

                if (ticksOutOfSun[pawnId] >= extension.ticksToResetDamage)
                {
                    if (accumulatedDamage.ContainsKey(pawnId))
                    {
                        accumulatedDamage.Remove(pawnId);
                        lastWarningTick.Remove(pawnId);
                        MoteMaker.ThrowText(pawn.DrawPos, map, "Sunlight damage reset", 2f);
                    }
                    ticksOutOfSun.Remove(pawnId);
                }
            }
        }

        private bool HasSunlightVulnerability(Pawn pawn)
        {
            if (pawn.genes == null) return false;

            return pawn.genes.GenesListForReading.Any(gene =>
                gene.Active && gene.def.GetModExtension<SunlightDamageExtension>() != null);
        }

        private SunlightDamageExtension GetSunlightExtension(Pawn pawn)
        {
            if (pawn.genes == null) return null;

            var gene = pawn.genes.GenesListForReading.FirstOrDefault(g =>
                g.Active && g.def.GetModExtension<SunlightDamageExtension>() != null);

            return gene?.def.GetModExtension<SunlightDamageExtension>();
        }

        private bool IsExposedToSunlight(Pawn pawn, SunlightDamageExtension extension)
        {
            if (map.skyManager.CurSkyGlow <= 0.5f || pawn.Position.Roofed(map))
            {
                return false;
            }

            if (extension.requireHeadCoverage && !HasHeadCoverage(pawn))
            {
                return true;
            }

            
            float totalCoverage = CalculateArmorCoverage(pawn);

            return totalCoverage < extension.minimumCoverageForProtection;
        }

        private bool HasHeadCoverage(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null || pawn.apparel.WornApparel.Count == 0)
            {
                return false;
            }

            
            List<string> headPartNames = new List<string>
            {
                "FullHead",
                "UpperHead",
                "Eyes"
            };

            foreach (string partName in headPartNames)
            {
                var bodyPart = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(partName);
                if (bodyPart != null)
                {
                    
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        if (apparel.def.apparel.bodyPartGroups.Contains(bodyPart))
                        {
                            return true; 
                        }
                    }
                }
            }

            return false; 
        }

        private float CalculateArmorCoverage(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null || pawn.apparel.WornApparel.Count == 0)
            {
                return 0f;
            }

            
            List<string> criticalBodyPartNames = new List<string>
            {
                "Torso",
                "Neck",
                "Shoulders",
                "Arms",
                "Legs",
                "FullHead",
                "UpperHead",
                "Eyes",
                "Face",
                "Hands",
                "Feet"
            };

            List<BodyPartGroupDef> criticalBodyParts = new List<BodyPartGroupDef>();

            
            foreach (string partName in criticalBodyPartNames)
            {
                var bodyPart = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail(partName);
                if (bodyPart != null)
                {
                    criticalBodyParts.Add(bodyPart);
                }
            }

            if (criticalBodyParts.Count == 0)
            {
                
                return pawn.apparel.WornApparel.Any(a => a.def.IsApparel) ? 0.8f : 0f;
            }

            float totalCoverage = 0f;
            int coveredParts = 0;

            foreach (BodyPartGroupDef bodyPartGroup in criticalBodyParts)
            {
                float partCoverage = 0f;

                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (apparel.def.apparel.bodyPartGroups.Contains(bodyPartGroup))
                    {
                        
                        float apparelCoverage = GetApparelCoverage(apparel);
                        partCoverage = Mathf.Max(partCoverage, apparelCoverage);
                    }
                }

                if (partCoverage > 0f)
                {
                    coveredParts++;
                    totalCoverage += partCoverage;
                }
            }

            
            if (criticalBodyParts.Count == 0) return 0f;

            
            float bodyPartsCoveredRatio = (float)coveredParts / criticalBodyParts.Count;
            float averagePartCoverage = coveredParts > 0 ? totalCoverage / coveredParts : 0f;

            
            return bodyPartsCoveredRatio * averagePartCoverage;
        }

        private float GetApparelCoverage(Apparel apparel)
        {
            
            ApparelLayerDef layer = apparel.def.apparel.LastLayer;

            if (layer == ApparelLayerDefOf.Shell || layer == ApparelLayerDefOf.Middle)
            {
                return 1.0f; 
            }
            else if (layer == ApparelLayerDefOf.OnSkin)
            {
                return 0.6f; 
            }

            
            if (apparel.def.apparel.bodyPartGroups.Count >= 2)
            {
                return 0.8f; 
            }

            return 0.5f; 
        }

        private void BuildSunlightMeter(Pawn pawn)
        {
            var extension = GetSunlightExtension(pawn);
            if (extension == null) return;

            if (burningEffecter != null)
            {
                burningEffecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
            }

            if (!accumulatedDamage.ContainsKey(pawn.thingIDNumber))
            {
                accumulatedDamage[pawn.thingIDNumber] = 0f;
            }
            accumulatedDamage[pawn.thingIDNumber] += extension.damagePerTick;

            int currentTick = Find.TickManager.TicksGame;
            float damagePercent = (accumulatedDamage[pawn.thingIDNumber] / extension.damageThresholdBeforeDeath) * 100f;

            
            if (currentTick % 1000 == 0)
            {
                float coverage = CalculateArmorCoverage(pawn);
                bool hasHeadCoverage = HasHeadCoverage(pawn);
                string headStatus = hasHeadCoverage ? "Protected" : "EXPOSED";
                MoteMaker.ThrowText(pawn.DrawPos, map, $"Head: {headStatus} | Coverage: {coverage:P0} | Exposure: {damagePercent:F1}%", 2f);
            }
            else if (currentTick % 500 == 0)
            {
                if (extension.requireHeadCoverage && !HasHeadCoverage(pawn))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, $"Head exposed! Damage: {damagePercent:F1}%", 2f);
                }
                else
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, $"Sunlight exposure: {damagePercent:F1}%", 2f);
                }
            }

            if (accumulatedDamage[pawn.thingIDNumber] >= extension.damageThresholdBeforeDeath * 0.8f)
            {
                if (!lastWarningTick.ContainsKey(pawn.thingIDNumber) ||
                    currentTick - lastWarningTick[pawn.thingIDNumber] >= 1000)
                {
                    string warningMessage = extension.requireHeadCoverage && !HasHeadCoverage(pawn)
                        ? "WARNING: Head exposed to sunlight!"
                        : "WARNING: High sunlight exposure!";
                    MoteMaker.ThrowText(pawn.DrawPos, map, warningMessage, 3f);
                    lastWarningTick[pawn.thingIDNumber] = currentTick;
                }
            }

            if (accumulatedDamage[pawn.thingIDNumber] >= extension.damageThresholdBeforeDeath)
            {
                KillPawn(pawn);
            }
        }

        public float GetSunlightDamageLevel(Pawn pawn)
        {
            if (accumulatedDamage.ContainsKey(pawn.thingIDNumber))
            {
                return accumulatedDamage[pawn.thingIDNumber];
            }
            return 0f;
        }

        public float GetSunlightDamagePercentage(Pawn pawn)
        {
            var extension = GetSunlightExtension(pawn);
            if (extension == null) return 0f;

            float currentDamage = GetSunlightDamageLevel(pawn);
            return (currentDamage / extension.damageThresholdBeforeDeath) * 100f;
        }

        public float GetArmorCoverage(Pawn pawn)
        {
            return CalculateArmorCoverage(pawn);
        }

        public bool GetHeadCoverage(Pawn pawn)
        {
            return HasHeadCoverage(pawn);
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
            ticksOutOfSun.Remove(pawn.thingIDNumber);
            lastWarningTick.Remove(pawn.thingIDNumber);
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