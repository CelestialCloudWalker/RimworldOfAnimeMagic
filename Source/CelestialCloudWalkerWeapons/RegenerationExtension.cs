using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Talented;
using System;

namespace AnimeArsenal
{
    public class RegenerationExtension : DefModExtension
    {
        public float healingMultiplier = 3.0f;
        public int ticksBetweenHealing = 60;
        public float healingPerTick = 1.0f;

        public bool instantLimbRegeneration = true;
        public bool instantOrganRegeneration = true;

        public bool canRegenerateOrgans = true;
        public bool canRegenerateBrain = false;
        public bool canRegenerateHeart = true;
        public bool canRegenerateHead = false;

        public float scarHealChance = 0.2f;
        public int scarHealInterval = 1500;

        // Resource consumption settings
        public bool consumeResourcesOnRegeneration = false;
        public float resourceCostPerHeal = 1f;
        public float resourceCostPerLimbRegen = 50f;
        public float resourceCostPerOrganRegen = 100f;
        public float resourceCostPerScarHeal = 25f;

        // Regeneration behavior when out of resources
        public bool preventRegenIfInsufficientResources = true;
        public float minimumResourcesRequired = 0.1f;
        public bool showResourceWarnings = true;
        public int resourceWarningCooldownTicks = 2500;

        // Death prevention settings
        public bool preventDeathFromVitalLoss = true;
        public List<string> fatalBodyParts = new List<string> { "Neck", "AA_DemonNeck" };

        // Notification settings
        public bool onlyNotifyMajorParts = true;

        // Text settings (simplified)
        public string resourceName = "Blood Art";
    }

    // Centralized helper class for all regeneration logic
    public static class RegenerationHelper
    {
        private static readonly HashSet<string> OrganTags = new HashSet<string>
        {
            "brain", "heart", "liver", "kidney", "lung", "stomach", "eye", "ear", "nose", "jaw", "tongue"
        };

        private static readonly HashSet<string> MajorParts = new HashSet<string>
        {
            "arm", "leg", "hand", "foot", "torso", "chest", "head", "heart", "brain",
            "liver", "kidney", "lung", "stomach", "shoulder", "pelvis", "spine", "ribcage"
        };

        private static readonly HashSet<string> PermanentInjuryKeywords = new HashSet<string>
        {
            "scar", "permanent", "old", "frail", "cataract", "hearing", "blindness",
            "asthma", "arthritis", "dementia", "alzheimer", "carcinoma", "fibrous", "cirrhosis"
        };

        public static string GetBasePartName(string partDefName)
        {
            if (partDefName.StartsWith("AA_Demon")) return partDefName.Substring(8);
            if (partDefName.StartsWith("AA_")) return partDefName.Substring(3);
            return partDefName;
        }

        public static bool IsPartType(string partDefName, string partType)
        {
            return GetBasePartName(partDefName).Equals(partType, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOrgan(BodyPartRecord part)
        {
            var basePartName = GetBasePartName(part.def.defName).ToLower();
            return OrganTags.Contains(basePartName) ||
                   part.def.tags.Any(tag => tag == BodyPartTagDefOf.BloodPumpingSource ||
                                           tag == BodyPartTagDefOf.BloodFiltrationSource ||
                                           tag == BodyPartTagDefOf.BreathingSource ||
                                           tag == BodyPartTagDefOf.ConsciousnessSource);
        }

        public static bool IsMajorPart(BodyPartRecord part)
        {
            return IsOrgan(part) || MajorParts.Contains(GetBasePartName(part.def.defName).ToLower());
        }

        public static bool IsPermanentInjury(Hediff hediff)
        {
            if (hediff.IsPermanent() || hediff.def.chronic) return true;

            var defName = hediff.def.defName.ToLower();
            return PermanentInjuryKeywords.Any(keyword => defName.Contains(keyword)) ||
                   (!hediff.def.tendable && !hediff.def.everCurableByItem && hediff.Severity > 0);
        }

        public static bool CanConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration) return true;

            var resourceGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)) as Gene_BasicResource;
            return resourceGene?.Value >= (cost + extension.minimumResourcesRequired);
        }

        public static bool TryConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration) return true;

            var resourceGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)) as Gene_BasicResource;
            if (resourceGene != null && resourceGene.Value >= (cost + extension.minimumResourcesRequired))
            {
                resourceGene.Consume(cost);
                return true;
            }
            return false;
        }
    }

    public class MapComponent_BloodDemonRegeneration : MapComponent
    {
        private Dictionary<int, int> lastResourceWarningTick = new Dictionary<int, int>();

        public MapComponent_BloodDemonRegeneration(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            var extension = DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)?.GetModExtension<RegenerationExtension>();
            if (extension == null || Find.TickManager.TicksGame % extension.ticksBetweenHealing != 0) return;

            var pawnsToProcess = map.mapPawns.AllPawnsSpawned
                .Where(p => HasBloodDemonArt(p) && !p.Dead && p?.Destroyed != true)
                .ToList();

            foreach (var pawn in pawnsToProcess)
            {
                ProcessPawn(pawn, extension);
            }
        }

        private void ProcessPawn(Pawn pawn, RegenerationExtension extension)
        {
            if (HasFatalDamage(pawn, extension))
            {
                KillPawnFromFatalDamage(pawn, extension);
                return;
            }

            ProcessRegeneration(pawn, extension);
            ProcessInstantRegeneration(pawn, extension);
            EnsurePawnIsUpright(pawn);
        }

        private bool HasBloodDemonArt(Pawn pawn)
        {
            return pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)) ?? false;
        }

        private bool HasFatalDamage(Pawn pawn, RegenerationExtension extension)
        {
            return pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Any(missingPart => extension.fatalBodyParts.Any(fatalPart =>
                    RegenerationHelper.IsPartType(missingPart.Part.def.defName, fatalPart)) ||
                    (!extension.canRegenerateHead && RegenerationHelper.IsPartType(missingPart.Part.def.defName, "Head")));
        }

        private void ProcessRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            var injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(injury => !injury.IsPermanent() && injury.Severity > 0)
                .ToList();

            bool anyBlocked = false;
            foreach (var injury in injuries)
            {
                float healAmount = extension.healingPerTick * extension.healingMultiplier;

                if (injury.Severity <= healAmount * 0.1f)
                {
                    injury.Heal(injury.Severity);
                    continue;
                }

                if (!RegenerationHelper.TryConsumeResource(pawn, extension, extension.resourceCostPerHeal))
                {
                    anyBlocked = true;
                    continue;
                }

                injury.Heal(healAmount);
                if (Rand.Chance(0.05f))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, "Regenerating...", Color.green, 2f);
                }
            }

            if (anyBlocked) ShowResourceWarning(pawn, extension);
        }

        private void ProcessInstantRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors().ToList();

            // Process limbs first
            if (extension.instantLimbRegeneration)
            {
                var limbToRegenerate = missingParts.FirstOrDefault(x => CanRegeneratePart(x.Part, extension, false));
                if (limbToRegenerate != null && RegenerationHelper.TryConsumeResource(pawn, extension, extension.resourceCostPerLimbRegen))
                {
                    RegeneratePart(pawn, limbToRegenerate.Part, extension, "Limb Regenerated!", Color.green);
                }
            }

            // Process organs
            if (extension.instantOrganRegeneration)
            {
                var organToRegenerate = missingParts.FirstOrDefault(x => CanRegeneratePart(x.Part, extension, true));
                if (organToRegenerate != null && RegenerationHelper.TryConsumeResource(pawn, extension, extension.resourceCostPerOrganRegen))
                {
                    RegeneratePart(pawn, organToRegenerate.Part, extension, "Organ Regenerated!", Color.cyan);
                }
            }
        }

        private bool CanRegeneratePart(BodyPartRecord part, RegenerationExtension extension, bool isOrgan)
        {
            // Check fatal parts
            if (extension.fatalBodyParts.Any(fatalPart => RegenerationHelper.IsPartType(part.def.defName, fatalPart)))
                return false;

            // Check head regeneration
            if (RegenerationHelper.IsPartType(part.def.defName, "Head") && !extension.canRegenerateHead)
                return false;

            if (isOrgan)
            {
                if (!extension.canRegenerateOrgans || !RegenerationHelper.IsOrgan(part)) return false;
                if (RegenerationHelper.IsPartType(part.def.defName, "Brain") && !extension.canRegenerateBrain) return false;
                if (RegenerationHelper.IsPartType(part.def.defName, "Heart") && !extension.canRegenerateHeart) return false;
                return true;
            }

            return !RegenerationHelper.IsOrgan(part); // Limbs are non-organs
        }

        private void RegeneratePart(Pawn pawn, BodyPartRecord part, RegenerationExtension extension, string moteText, Color moteColor)
        {
            var missingPart = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().FirstOrDefault(h => h.Part == part);
            if (missingPart != null)
            {
                pawn.health.RemoveHediff(missingPart);

                // Add minor wound
                var freshWound = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
                freshWound.Severity = 0.01f;
                pawn.health.AddHediff(freshWound);
            }

            RefreshPawn(pawn);

            // Show notifications for major parts only
            if (!extension.onlyNotifyMajorParts || RegenerationHelper.IsMajorPart(part))
            {
                var partName = RegenerationHelper.GetBasePartName(part.def.defName);
                Messages.Message($"{pawn.LabelShort} has instantly regenerated their {partName}!", pawn, MessageTypeDefOf.PositiveEvent);
                MoteMaker.ThrowText(pawn.DrawPos, map, moteText, moteColor, 4f);
            }
        }

        private void EnsurePawnIsUpright(Pawn pawn)
        {
            if (!pawn.Downed || pawn.Dead) return;

            try
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();

                float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                float moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
                float bloodPumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);

                if (consciousness > 0.3f && moving > 0.2f && bloodPumping > 0.3f)
                {
                    // Reduce major injuries
                    var majorInjuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                        .Where(injury => injury.Severity > 15f).ToList();

                    foreach (var injury in majorInjuries)
                    {
                        injury.Heal(injury.Severity * 0.5f);
                        if (injury.Severity < 1f) pawn.health.RemoveHediff(injury);
                    }

                    pawn.health.capacities.Notify_CapacityLevelsDirty();
                    pawn.health.summaryHealth.Notify_HealthChanged();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error ensuring pawn {pawn.LabelShort} is upright: {ex}");
            }
        }

        private void ShowResourceWarning(Pawn pawn, RegenerationExtension extension)
        {
            if (!extension.showResourceWarnings) return;

            int currentTick = Find.TickManager.TicksGame;
            int pawnId = pawn.thingIDNumber;

            if (lastResourceWarningTick.ContainsKey(pawnId) &&
                currentTick - lastResourceWarningTick[pawnId] < extension.resourceWarningCooldownTicks)
                return;

            if (Rand.Chance(0.02f))
            {
                MoteMaker.ThrowText(pawn.DrawPos, map, $"Low {extension.resourceName}!", Color.yellow, 2f);
                lastResourceWarningTick[pawnId] = currentTick;
            }
        }

        private void RefreshPawn(Pawn pawn)
        {
            try
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();
                if (pawn.Spawned) pawn.Notify_ColorChanged();
                PortraitsCache.SetDirty(pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"Error refreshing pawn {pawn.LabelShort}: {ex}");
            }
        }

        private void KillPawnFromFatalDamage(Pawn pawn, RegenerationExtension extension)
        {
            var fatalPart = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(mp => extension.fatalBodyParts.Any(fp => RegenerationHelper.IsPartType(mp.Part.def.defName, fp)))
                .Select(mp => RegenerationHelper.GetBasePartName(mp.Part.def.defName).ToLower())
                .FirstOrDefault() ?? "vital part";

            Messages.Message($"{pawn.LabelShort} dies from catastrophic {fatalPart} damage - even regeneration cannot save them.",
                pawn, MessageTypeDefOf.NegativeEvent);
            MoteMaker.ThrowText(pawn.DrawPos, map, $"Fatal {fatalPart} Damage!", Color.red, 5f);
            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 5);
            pawn.Kill(null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastResourceWarningTick, "lastResourceWarningTick", LookMode.Value, LookMode.Value);

            if (lastResourceWarningTick == null)
            {
                lastResourceWarningTick = new Dictionary<int, int>();
            }
        }
    }

    public class BloodDemonRegenerationGene : Gene
    {
        private int lastScarHealCheck = 0;

        public override void Tick()
        {
            base.Tick();

            var extension = def.GetModExtension<RegenerationExtension>();
            if (extension == null) return;

            if (Find.TickManager.TicksGame - lastScarHealCheck >= extension.scarHealInterval)
            {
                lastScarHealCheck = Find.TickManager.TicksGame;
                ProcessScarHealing(extension);
            }
        }

        private void ProcessScarHealing(RegenerationExtension extension)
        {
            var permanentInjury = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => RegenerationHelper.IsPermanentInjury(h));

            if (permanentInjury != null && Rand.Chance(extension.scarHealChance) &&
                RegenerationHelper.TryConsumeResource(pawn, extension, extension.resourceCostPerScarHeal))
            {
                pawn.health.RemoveHediff(permanentInjury);

                if (pawn.Map != null)
                {
                    var healedType = permanentInjury.def.label ?? "Permanent Injury";
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{healedType} healed", Color.blue, 2f);

                    // Show message for important injuries
                    var defName = permanentInjury.def.defName.ToLower();
                    if (defName.Contains("blindness") || defName.Contains("hearing") || defName.Contains("dementia"))
                    {
                        Messages.Message($"{pawn.LabelShort}'s {healedType} has been completely healed by regeneration!",
                            pawn, MessageTypeDefOf.PositiveEvent);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastScarHealCheck, "lastScarHealCheck", 0);
        }
    }
}