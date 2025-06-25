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

        // NEW: Resource warning throttling settings
        public int resourceWarningCooldownTicks = 2500; // ~1 minute at 60 TPS
        public float resourceWarningChance = 0.02f; // Reduced from 0.1f to 0.02f

        // Death prevention settings
        public bool preventDeathFromVitalLoss = true;
        public List<string> fatalBodyParts = new List<string> { "Neck", "AA_DemonNeck" };

        // Notification settings for major parts only
        public bool onlyNotifyMajorParts = true;
        public List<string> majorBodyParts = new List<string>
        {
            "Arm", "Leg", "Hand", "Foot", "Torso", "Chest", "Head",
            "Heart", "Brain", "Liver", "Kidney", "Lung", "Stomach",
            "Shoulder", "Pelvis", "Spine", "Ribcage"
        };

        // ===== CUSTOMIZABLE TEXT SETTINGS =====

        // General regeneration text
        public string regeneratingText = "Regenerating...";
        public string lowResourceText = "Low {0}!"; // {0} will be replaced with resource name

        // Limb regeneration text
        public string limbRegeneratedText = "Limb Regenerated!";
        public string limbRegeneratedMessage = "{0} has instantly regenerated their {1}!"; // {0} = pawn name, {1} = part name
        public string insufficientResourcesLimbMessage = "{0} cannot regenerate limb - insufficient {1}!"; // {0} = pawn name, {1} = resource name

        // Organ regeneration text
        public string organRegeneratedText = "Organ Regenerated!";
        public string organRegeneratedMessage = "{0} has instantly regenerated their {1}!"; // {0} = pawn name, {1} = part name
        public string insufficientResourcesOrganMessage = "{0} cannot regenerate organ - insufficient {1}!"; // {0} = pawn name, {1} = resource name

        // Special organ text
        public string heartRegeneratedText = "Heart beats again!";
        public string brainRegeneratedText = "Mind restored!";

        // Scar healing text
        public string scarHealedText = "{0} healed"; // {0} = injury type
        public string importantScarHealedMessage = "{0}'s {1} has been completely healed by regeneration!"; // {0} = pawn name, {1} = injury type

        // Death text
        public string fatalDamageMessage = "{0} dies from catastrophic {1} damage - even regeneration cannot save them."; // {0} = pawn name, {1} = part name
        public string fatalDamageText = "Fatal {0} Damage!"; // {0} = part name

        // Resource name for messages
        public string resourceName = "Blood Art";
    }

    public class MapComponent_BloodDemonRegeneration : MapComponent
    {
        // NEW: Dictionary to track last warning time for each pawn
        private Dictionary<int, int> lastResourceWarningTick = new Dictionary<int, int>();

        public MapComponent_BloodDemonRegeneration(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            var extension = DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)
                ?.GetModExtension<RegenerationExtension>();
            if (extension == null) return;

            if (Find.TickManager.TicksGame % extension.ticksBetweenHealing != 0) return;

            List<Pawn> pawnsToProcess = map.mapPawns.AllPawnsSpawned
                .Where(p => HasBloodDemonArt(p) && !p.Dead)
                .ToList();

            foreach (Pawn pawn in pawnsToProcess)
            {
                if (pawn?.Destroyed != true)
                {
                    // Check for fatal damage first
                    if (HasFatalDamage(pawn, extension))
                    {
                        KillPawnFromFatalDamage(pawn, extension);
                        continue;
                    }

                    // Process regeneration
                    ProcessRegeneration(pawn, extension);
                    ProcessInstantLimbRegeneration(pawn, extension);
                    ProcessInstantOrganRegeneration(pawn, extension);

                    // IMPORTANT: Ensure pawn doesn't stay downed after regeneration
                    EnsurePawnIsUpright(pawn);
                }
            }
        }

        private bool HasBloodDemonArt(Pawn pawn)
        {
            return pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)) ?? false;
        }

        private bool HasFatalDamage(Pawn pawn, RegenerationExtension extension)
        {
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();

            foreach (var missingPart in missingParts)
            {
                string partName = missingPart.Part.def.defName;

                // Check if this part is in the fatal parts list
                if (extension.fatalBodyParts.Any(fatalPart =>
                    IsPartType(partName, fatalPart)))
                {
                    return true;
                }

                // Special check for head if not regenerable
                if (!extension.canRegenerateHead && IsPartType(partName, "Head"))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsurePawnIsUpright(Pawn pawn)
        {
            try
            {
                // Force recalculation of capacities
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();

                // Check if pawn should be able to move but is downed
                if (pawn.Downed && !pawn.Dead)
                {
                    float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                    float moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
                    float bloodPumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);

                    // If vital capacities are sufficient, force pawn to get up
                    if (consciousness > 0.3f && moving > 0.2f && bloodPumping > 0.3f)
                    {
                        // Remove any downing injuries that might be keeping them down
                        RemoveDowningInjuries(pawn);

                        // Force recalculation again
                        pawn.health.capacities.Notify_CapacityLevelsDirty();
                        pawn.health.summaryHealth.Notify_HealthChanged();

                        // If still downed, log for debugging
                        if (pawn.Downed)
                        {
                            Log.Warning($"Blood Demon {pawn.LabelShort} still downed despite sufficient capacities. Consciousness: {consciousness}, Moving: {moving}, BloodPumping: {bloodPumping}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error ensuring pawn {pawn.LabelShort} is upright: {ex}");
            }
        }

        private void RemoveDowningInjuries(Pawn pawn)
        {
            var downingSeverities = new List<Hediff>();

            foreach (var hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff is Hediff_Injury injury && injury.Severity > 0)
                {
                    // Reduce severity of major injuries that might be causing downing
                    if (injury.Severity > 15f) // Major injury threshold
                    {
                        float reduction = injury.Severity * 0.5f; // Reduce by 50%
                        injury.Heal(reduction);

                        if (injury.Severity < 1f)
                        {
                            downingSeverities.Add(injury);
                        }
                    }
                }
            }

            // Remove very minor injuries
            foreach (var hediff in downingSeverities)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private bool CanConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration)
                return true;

            Gene foundGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false));
            if (foundGene != null && foundGene is Gene_BasicResource resourceGene)
            {
                return resourceGene.Value >= (cost + extension.minimumResourcesRequired);
            }
            return false;
        }

        private bool TryConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration)
                return true;

            Gene foundGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false));
            if (foundGene != null && foundGene is Gene_BasicResource resourceGene)
            {
                if (resourceGene.Value >= (cost + extension.minimumResourcesRequired))
                {
                    resourceGene.Consume(cost);
                    return true;
                }
            }
            return false;
        }

        // NEW: Improved resource warning system with throttling
        private void ShowResourceWarning(Pawn pawn, RegenerationExtension extension)
        {
            if (!extension.showResourceWarnings) return;

            int currentTick = Find.TickManager.TicksGame;
            int pawnId = pawn.thingIDNumber;

            // Check if enough time has passed since last warning for this pawn
            if (lastResourceWarningTick.ContainsKey(pawnId))
            {
                int ticksSinceLastWarning = currentTick - lastResourceWarningTick[pawnId];
                if (ticksSinceLastWarning < extension.resourceWarningCooldownTicks)
                {
                    return; // Still on cooldown
                }
            }

            // Show warning with reduced chance
            if (Rand.Chance(extension.resourceWarningChance))
            {
                string text = string.Format(extension.lowResourceText, extension.resourceName);
                MoteMaker.ThrowText(pawn.DrawPos, map, text, Color.yellow, 2f);

                // Update the last warning time
                lastResourceWarningTick[pawnId] = currentTick;
            }
        }

        private void ProcessRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            var injuries = pawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury injury && !injury.IsPermanent())
                .Cast<Hediff_Injury>()
                .Where(injury => injury.Severity > 0)
                .ToList();

            bool anyRegenerationBlocked = false;

            foreach (var injury in injuries)
            {
                float healAmount = extension.healingPerTick * extension.healingMultiplier;

                if (injury.Severity > healAmount * 0.1f)
                {
                    if (extension.preventRegenIfInsufficientResources &&
                        !CanConsumeResource(pawn, extension, extension.resourceCostPerHeal))
                    {
                        anyRegenerationBlocked = true;
                        continue;
                    }

                    if (TryConsumeResource(pawn, extension, extension.resourceCostPerHeal))
                    {
                        injury.Heal(healAmount);

                        if (Rand.Chance(0.05f))
                        {
                            MoteMaker.ThrowText(pawn.DrawPos, map, extension.regeneratingText, Color.green, 2f);
                        }
                    }
                    else
                    {
                        anyRegenerationBlocked = true;
                    }
                }
                else if (injury.Severity > 0)
                {
                    injury.Heal(injury.Severity);
                }
            }

            // Show warning only once per tick if any regeneration was blocked
            if (anyRegenerationBlocked)
            {
                ShowResourceWarning(pawn, extension);
            }
        }

        private void ProcessInstantLimbRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            if (!extension.instantLimbRegeneration) return;

            var missingPartHediffs = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(x => CanRegenerateLimb(x.Part, extension))
                .ToList();

            foreach (var missingPartHediff in missingPartHediffs)
            {
                if (extension.preventRegenIfInsufficientResources &&
                    !CanConsumeResource(pawn, extension, extension.resourceCostPerLimbRegen))
                {
                    // Show individual message for limb regeneration failure (less frequent)
                    if (extension.showResourceWarnings && Rand.Chance(0.01f)) // Reduced from 0.05f
                    {
                        string message = string.Format(extension.insufficientResourcesLimbMessage,
                            pawn.LabelShort, extension.resourceName);
                        Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent);
                    }
                    continue;
                }

                if (TryConsumeResource(pawn, extension, extension.resourceCostPerLimbRegen))
                {
                    RegenerateLimb(pawn, missingPartHediff.Part, extension);
                }
                break;
            }
        }

        private void ProcessInstantOrganRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            if (!extension.canRegenerateOrgans || !extension.instantOrganRegeneration) return;

            var missingOrganHediffs = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(x => CanRegenerateOrgan(x.Part, extension))
                .ToList();

            foreach (var missingOrganHediff in missingOrganHediffs)
            {
                if (extension.preventRegenIfInsufficientResources &&
                    !CanConsumeResource(pawn, extension, extension.resourceCostPerOrganRegen))
                {
                    // Show individual message for organ regeneration failure (less frequent)
                    if (extension.showResourceWarnings && Rand.Chance(0.01f)) // Reduced from 0.05f
                    {
                        string message = string.Format(extension.insufficientResourcesOrganMessage,
                            pawn.LabelShort, extension.resourceName);
                        Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent);
                    }
                    continue;
                }

                if (TryConsumeResource(pawn, extension, extension.resourceCostPerOrganRegen))
                {
                    RegenerateOrgan(pawn, missingOrganHediff.Part, extension);
                }
                break;
            }
        }

        private bool CanRegenerateLimb(BodyPartRecord part, RegenerationExtension extension)
        {
            // Check if this part is fatal and shouldn't be regenerated
            if (extension.fatalBodyParts.Any(fatalPart => IsPartType(part.def.defName, fatalPart)))
            {
                return false;
            }

            // Check head regeneration setting
            if (IsPartType(part.def.defName, "Head") && !extension.canRegenerateHead)
            {
                return false;
            }

            // Allow torso regeneration (this is key for preventing regen lock)
            if (IsPartType(part.def.defName, "Torso") ||
                IsPartType(part.def.defName, "Chest") ||
                part.def.defName.ToLower().Contains("torso"))
            {
                return true;
            }

            // Allow other limbs but exclude organs
            return !part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource) &&
                   !IsOrgan(part);
        }

        private bool CanRegenerateOrgan(BodyPartRecord part, RegenerationExtension extension)
        {
            if (!extension.canRegenerateOrgans || !IsOrgan(part)) return false;

            // Check fatal parts
            if (extension.fatalBodyParts.Any(fatalPart => IsPartType(part.def.defName, fatalPart)))
            {
                return false;
            }

            if (IsPartType(part.def.defName, "Brain") && !extension.canRegenerateBrain) return false;
            if (IsPartType(part.def.defName, "Heart") && !extension.canRegenerateHeart) return false;
            if (IsPartType(part.def.defName, "Head") && !extension.canRegenerateHead) return false;

            return true;
        }

        private bool IsOrgan(BodyPartRecord part)
        {
            bool hasOrganTags = part.def.tags.Any(tag =>
                       tag == BodyPartTagDefOf.BloodPumpingSource ||
                       tag == BodyPartTagDefOf.BloodFiltrationSource ||
                       tag == BodyPartTagDefOf.BreathingSource ||
                       tag == BodyPartTagDefOf.EatingSource ||
                       tag == BodyPartTagDefOf.HearingSource ||
                       tag == BodyPartTagDefOf.SightSource ||
                       tag == BodyPartTagDefOf.ConsciousnessSource);

            bool isCommonOrgan = IsPartType(part.def.defName, "Brain") ||
                                IsPartType(part.def.defName, "Heart") ||
                                IsPartType(part.def.defName, "Liver") ||
                                IsPartType(part.def.defName, "Kidney") ||
                                IsPartType(part.def.defName, "Lung") ||
                                IsPartType(part.def.defName, "Stomach") ||
                                IsPartType(part.def.defName, "Eye") ||
                                IsPartType(part.def.defName, "Ear") ||
                                IsPartType(part.def.defName, "Nose") ||
                                IsPartType(part.def.defName, "Jaw") ||
                                IsPartType(part.def.defName, "Tongue");

            return hasOrganTags || isCommonOrgan;
        }

        private bool IsPartType(string partDefName, string partType)
        {
            string basePartName = GetBasePartName(partDefName);
            return basePartName.Equals(partType, System.StringComparison.OrdinalIgnoreCase);
        }

        private string GetBasePartName(string partDefName)
        {
            if (partDefName.StartsWith("AA_Demon"))
            {
                return partDefName.Substring(8);
            }

            if (partDefName.StartsWith("AA_"))
            {
                return partDefName.Substring(3);
            }

            return partDefName;
        }

        // NEW METHOD: Check if a body part should trigger notifications
        private bool IsMajorBodyPart(BodyPartRecord part, RegenerationExtension extension)
        {
            if (!extension.onlyNotifyMajorParts)
                return true; // Show all notifications if setting is disabled

            string basePartName = GetBasePartName(part.def.defName);

            // Always notify for organs
            if (IsOrgan(part))
                return true;

            // Check if it's in the major parts list
            return extension.majorBodyParts.Any(majorPart =>
                IsPartType(part.def.defName, majorPart));
        }

        private void RegenerateLimb(Pawn pawn, BodyPartRecord part, RegenerationExtension extension)
        {
            var missingPart = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missingPart != null)
            {
                pawn.health.RemoveHediff(missingPart);
            }

            // Add only a minor fresh wound instead of a major one
            var freshWound = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            freshWound.Severity = 0.01f; // Very minor wound
            pawn.health.AddHediff(freshWound);

            RefreshPawnAfterRegeneration(pawn);

            // Only show notification for major parts
            if (IsMajorBodyPart(part, extension))
            {
                string partDisplayName = GetDisplayPartName(part.def.defName);
                string message = string.Format(extension.limbRegeneratedMessage, pawn.LabelShort, partDisplayName);
                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);

                MoteMaker.ThrowText(pawn.DrawPos, map, extension.limbRegeneratedText, Color.green, 4f);
            }
        }

        private void RegenerateOrgan(Pawn pawn, BodyPartRecord part, RegenerationExtension extension)
        {
            var missingPart = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missingPart != null)
            {
                pawn.health.RemoveHediff(missingPart);
            }

            RefreshPawnAfterRegeneration(pawn);

            // Only show notification for major parts (organs are always considered major)
            if (IsMajorBodyPart(part, extension))
            {
                string partDisplayName = GetDisplayPartName(part.def.defName);
                string message = string.Format(extension.organRegeneratedMessage, pawn.LabelShort, partDisplayName);
                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);

                MoteMaker.ThrowText(pawn.DrawPos, map, extension.organRegeneratedText, Color.cyan, 4f);

                if (IsPartType(part.def.defName, "Heart"))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, extension.heartRegeneratedText, Color.red, 5f);
                }
                else if (IsPartType(part.def.defName, "Brain"))
                {
                    MoteMaker.ThrowText(pawn.DrawPos, map, extension.brainRegeneratedText, Color.magenta, 5f);
                }
            }
        }

        private string GetDisplayPartName(string partDefName)
        {
            if (partDefName.StartsWith("AA_Demon"))
            {
                string baseName = partDefName.Substring(8);
                return $"Demon {baseName}";
            }

            if (partDefName.StartsWith("AA_"))
            {
                string baseName = partDefName.Substring(3);
                return $"Demon {baseName}";
            }

            return partDefName;
        }

        private void RefreshPawnAfterRegeneration(Pawn pawn)
        {
            try
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();

                if (pawn.Spawned)
                {
                    pawn.Notify_ColorChanged();
                }

                PortraitsCache.SetDirty(pawn);

                // Additional refresh to ensure capacities are properly updated
                pawn.health.capacities.Notify_CapacityLevelsDirty();
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error refreshing pawn {pawn.LabelShort} after regeneration: {ex}");
            }
        }

        private void KillPawnFromFatalDamage(Pawn pawn, RegenerationExtension extension)
        {
            string fatalPart = "vital part";
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();

            foreach (var missingPart in missingParts)
            {
                string partName = missingPart.Part.def.defName;
                if (extension.fatalBodyParts.Any(fatalPartName => IsPartType(partName, fatalPartName)))
                {
                    fatalPart = GetDisplayPartName(partName).ToLower();
                    break;
                }
            }

            string message = string.Format(extension.fatalDamageMessage, pawn.LabelShort, fatalPart);
            Messages.Message(message, pawn, MessageTypeDefOf.NegativeEvent);

            string text = string.Format(extension.fatalDamageText, fatalPart);
            MoteMaker.ThrowText(pawn.DrawPos, map, text, Color.red, 5f);

            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 5);
            pawn.Kill(null);
        }

        // NEW: Clean up tracking dictionary when pawns are removed
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
            var permanentInjuries = pawn.health.hediffSet.hediffs
                .Where(h => IsPermanentInjury(h))
                .ToList();

            foreach (var injury in permanentInjuries.Take(1))
            {
                if (Rand.Chance(extension.scarHealChance))
                {
                    // Check resource cost for scar healing
                    if (extension.preventRegenIfInsufficientResources &&
                        !CanConsumeResource(pawn, extension, extension.resourceCostPerScarHeal))
                    {
                        continue;
                    }

                    if (TryConsumeResource(pawn, extension, extension.resourceCostPerScarHeal))
                    {
                        pawn.health.RemoveHediff(injury);
                        if (pawn.Map != null)
                        {
                            string healedType = GetInjuryTypeName(injury);
                            string text = string.Format(extension.scarHealedText, healedType);
                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, text, Color.blue, 2f);

                            if (IsImportantInjury(injury))
                            {
                                string message = string.Format(extension.importantScarHealedMessage,
                                    pawn.LabelShort, healedType);
                                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
                            }
                        }
                    }
                    break;
                }
            }
        }

        private bool CanConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration)
                return true;

            Gene foundGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false));
            if (foundGene != null && foundGene is Gene_BasicResource resourceGene)
            {
                return resourceGene.Value >= (cost + extension.minimumResourcesRequired);
            }
            return false;
        }

        private bool TryConsumeResource(Pawn pawn, RegenerationExtension extension, float cost)
        {
            if (!extension.consumeResourcesOnRegeneration)
                return true;

            Gene foundGene = pawn.genes?.GetGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false));
            if (foundGene != null && foundGene is Gene_BasicResource resourceGene)
            {
                if (resourceGene.Value >= (cost + extension.minimumResourcesRequired))
                {
                    resourceGene.Consume(cost);
                    return true;
                }
            }
            return false;
        }

        private bool IsPermanentInjury(Hediff hediff)
        {
            string defName = hediff.def.defName.ToLower();

            return hediff.IsPermanent() ||
                   hediff.def.chronic ||
                   defName.Contains("scar") || defName.Contains("permanent") || defName.Contains("old") ||
                   defName.Contains("frail") || defName.Contains("cataract") || defName.Contains("hearing") ||
                   defName.Contains("blindness") || defName.Contains("asthma") || defName.Contains("arthritis") ||
                   defName.Contains("dementia") || defName.Contains("alzheimer") || defName.Contains("carcinoma") ||
                   defName.Contains("fibrous") || defName.Contains("cirrhosis") ||
                   (!hediff.def.tendable && !hediff.def.everCurableByItem && hediff.Severity > 0);
        }

        private string GetInjuryTypeName(Hediff hediff)
        {
            string defName = hediff.def.defName.ToLower();
            if (defName.Contains("scar")) return "Scar";
            if (defName.Contains("cataract")) return "Cataract";
            if (defName.Contains("hearing")) return "Hearing Loss";
            if (defName.Contains("blindness")) return "Blindness";
            if (defName.Contains("asthma")) return "Asthma";
            if (defName.Contains("arthritis")) return "Arthritis";
            if (defName.Contains("dementia")) return "Dementia";
            if (defName.Contains("frail")) return "Frailty";
            if (defName.Contains("fibrous")) return "Fibrous Mechanites";
            if (defName.Contains("sensory")) return "Sensory Mechanites";
            return hediff.def.label ?? "Permanent Injury";
        }

        private bool IsImportantInjury(Hediff hediff)
        {
            string defName = hediff.def.defName.ToLower();
            return defName.Contains("blindness") || defName.Contains("hearing") || defName.Contains("dementia") ||
                   defName.Contains("alzheimer") || defName.Contains("frail") || defName.Contains("arthritis") ||
                   defName.Contains("asthma") || defName.Contains("carcinoma") || defName.Contains("cirrhosis") ||
                   defName.Contains("fibrous") || defName.Contains("sensory");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastScarHealCheck, "lastScarHealCheck", 0);
        }
    }
}