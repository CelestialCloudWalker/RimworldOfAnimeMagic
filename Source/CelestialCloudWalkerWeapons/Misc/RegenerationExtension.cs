using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using System;
using EMF;

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

        public bool consumeResourcesOnRegeneration = false;
        public float resourceCostPerHeal = 1f;
        public float resourceCostPerLimbRegen = 50f;
        public float resourceCostPerOrganRegen = 100f;
        public float resourceCostPerScarHeal = 25f;

        public bool preventRegenIfInsufficientResources = true;
        public float minimumResourcesRequired = 0.1f;
        public bool showResourceWarnings = true;
        public int resourceWarningCooldownTicks = 2500;

        public bool preventDeathFromVitalLoss = true;
        public List<string> fatalBodyParts = new List<string> { "Neck", "AA_DemonNeck" };

        public bool onlyNotifyMajorParts = true;

        public string resourceName = "Blood Art";

        public List<string> protectedHediffPrefixes = new List<string>
        {
            "DS_", "AA_", "RimKetsu_"
        };
    }

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

        private static readonly HashSet<string> PermanentKeywords = new HashSet<string>
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

        public static float GetSanityMultiplier(Pawn pawn)
        {
            var demonGene = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene) as BloodDemonArtsGene;

            if (demonGene == null) return 1f;

            return demonGene.RegenMultiplier;
        }

        public static bool IsOrgan(BodyPartRecord part)
        {
            var baseName = GetBasePartName(part.def.defName).ToLower();

            if (OrganTags.Contains(baseName))
                return true;

            if (part.def.tags.Any(tag =>
                tag == BodyPartTagDefOf.BloodPumpingSource ||
                tag == BodyPartTagDefOf.BloodFiltrationSource ||
                tag == BodyPartTagDefOf.BreathingSource ||
                tag == BodyPartTagDefOf.ConsciousnessSource ||
                tag == BodyPartTagDefOf.SightSource ||
                tag == BodyPartTagDefOf.HearingSource ||
                tag == BodyPartTagDefOf.TalkingSource ||
                tag == BodyPartTagDefOf.EatingSource))
                return true;

            if (baseName.Contains("eye") || baseName.Contains("ear") ||
                baseName.Contains("heart") || baseName.Contains("brain") ||
                baseName.Contains("lung") || baseName.Contains("kidney") ||
                baseName.Contains("liver") || baseName.Contains("stomach"))
                return true;

            return false;
        }

        public static bool IsMajorPart(BodyPartRecord part)
        {
            return IsOrgan(part) || MajorParts.Contains(GetBasePartName(part.def.defName).ToLower());
        }

        public static bool IsPermanentInjury(Hediff hediff, RegenerationExtension ext)
        {
            if (ext?.protectedHediffPrefixes != null)
            {
                foreach (var prefix in ext.protectedHediffPrefixes)
                {
                    if (hediff.def.defName.StartsWith(prefix))
                        return false;
                }
            }

            if (hediff is HediffWithComps hwc &&
                hwc.comps != null &&
                hwc.comps.Any(c => c is HediffComp_Disappears))
                return false;

            if (hediff.def.hediffClass != null)
            {
                var className = hediff.def.hediffClass.Name;
                if (className.Contains("Ability") || className.Contains("Gene"))
                    return false;
            }

            if (hediff.def.tendable || hediff.def.everCurableByItem)
                return false;

            if (hediff.IsPermanent() || hediff.def.chronic)
                return true;

            var defName = hediff.def.defName.ToLower();
            if (PermanentKeywords.Any(keyword => defName.Contains(keyword)))
                return true;

            if (!hediff.def.tendable &&
                !hediff.def.everCurableByItem &&
                hediff.Severity > 0 &&
                hediff.Part != null)
                return true;

            return false;
        }

        public static bool CanConsumeResource(Pawn pawn, RegenerationExtension ext, float cost)
        {
            if (!ext.consumeResourcesOnRegeneration) return true;
            var gene = GetResourceGene(pawn);
            if (gene == null) return false;
            return gene.Value >= (cost + ext.minimumResourcesRequired * gene.Max);
        }

        public static bool TryConsumeResource(Pawn pawn, RegenerationExtension ext, float cost)
        {
            if (!ext.consumeResourcesOnRegeneration) return true;
            var gene = GetResourceGene(pawn);
            if (gene == null) return false;
            if (gene.Value >= (cost + ext.minimumResourcesRequired * gene.Max))
            {
                gene.Consume(cost);
                return true;
            }
            return false;
        }

        private static EMF.Gene_BasicResource GetResourceGene(Pawn pawn)
        {
            return pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g.Active && g.def.GetModExtension<RegenerationExtension>() != null)
                as EMF.Gene_BasicResource;
        }
    }

    public class MapComponent_BloodDemonRegeneration : MapComponent
    {
        private Dictionary<int, int> lastResourceWarn = new Dictionary<int, int>();
        private HashSet<int> pendingInstantRegen = new HashSet<int>();

        public MapComponent_BloodDemonRegeneration(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            var pawnsToProcess = map.mapPawns.AllPawnsSpawned
                .Where(p => HasRegenGene(p) && !p.Dead)
                .ToList();

            foreach (var pawn in pawnsToProcess)
            {
                var ext = GetRegenExtension(pawn);
                if (ext != null && Find.TickManager.TicksGame % ext.ticksBetweenHealing == 0)
                {
                    ProcessPawn(pawn, ext);
                }
            }
        }

        private void ProcessPawn(Pawn pawn, RegenerationExtension ext)
        {
            if (HasFatalDamage(pawn, ext))
            {
                KillFromFatalDamage(pawn, ext);
                return;
            }

            RegenSuppressionState suppression = RegenSuppressionTracker.GetState(pawn);

            ProcessNormalRegen(pawn, ext, suppression);
            ProcessInstantRegen(pawn, ext, suppression);
            TryStandUp(pawn);
        }

        private bool HasRegenGene(Pawn pawn)
        {
            return pawn.genes?.GenesListForReading.Any(g =>
                g.Active && g.def.GetModExtension<RegenerationExtension>() != null) ?? false;
        }

        private RegenerationExtension GetRegenExtension(Pawn pawn)
        {
            return pawn.genes?.GenesListForReading.FirstOrDefault(g =>
                g.Active && g.def.GetModExtension<RegenerationExtension>() != null)
                ?.def.GetModExtension<RegenerationExtension>();
        }

        private bool HasFatalDamage(Pawn pawn, RegenerationExtension ext)
        {
            return pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Any(mp =>
                    ext.fatalBodyParts.Any(fp =>
                        RegenerationHelper.IsPartType(mp.Part.def.defName, fp)) ||
                    (!ext.canRegenerateHead &&
                        RegenerationHelper.IsPartType(mp.Part.def.defName, "Head")));
        }

        private void ProcessNormalRegen(Pawn pawn, RegenerationExtension ext,
            RegenSuppressionState suppression)
        {
            if (suppression.suppressWound && suppression.regenMultiplier <= 0f)
                return;

            var injuries = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(i => !i.IsPermanent() && i.Severity > 0)
                .ToList();

            bool blocked = false;
            float sanityMultiplier = RegenerationHelper.GetSanityMultiplier(pawn);

            float effectiveMultiplier = sanityMultiplier;
            if (suppression.suppressWound)
                effectiveMultiplier *= suppression.regenMultiplier;

            foreach (var injury in injuries)
            {
                float heal = ext.healingPerTick * ext.healingMultiplier * effectiveMultiplier;

                if (heal <= 0f) continue;

                if (injury.Severity <= heal * 0.1f)
                {
                    injury.Heal(injury.Severity);
                    continue;
                }

                if (!RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerHeal))
                {
                    blocked = true;
                    continue;
                }

                injury.Heal(heal);

                if (Rand.Chance(0.05f))
                {
                    Color moteColor;
                    string moteText;

                    if (suppression.AnyEffect)
                    {
                        moteColor = Color.red;
                        moteText = suppression.isRecovering ? "Recovering..." : "Suppressed...";
                    }
                    else if (sanityMultiplier >= 1f)
                    {
                        moteColor = Color.green;
                        moteText = "Regenerating...";
                    }
                    else
                    {
                        moteColor = Color.yellow;
                        moteText = "Weak Regen...";
                    }

                    MoteMaker.ThrowText(pawn.DrawPos, map, moteText, moteColor, 2f);
                }
            }

            if (blocked) ShowResourceWarning(pawn, ext);
        }

        private void ProcessInstantRegen(Pawn pawn, RegenerationExtension ext,
            RegenSuppressionState suppression)
        {
            int pawnId = pawn.thingIDNumber;

            if (!suppression.suppressScar)
            {
                var permanentInjuries = pawn.health.hediffSet.hediffs
                    .Where(h => RegenerationHelper.IsPermanentInjury(h, ext) && h.Visible)
                    .ToList();

                if (permanentInjuries.Count > 0)
                {
                    var injury = permanentInjuries.RandomElement();

                    if (RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerScarHeal))
                    {
                        pawn.health.RemoveHediff(injury);

                        if (!ext.onlyNotifyMajorParts ||
                            (injury.Part != null && RegenerationHelper.IsMajorPart(injury.Part)))
                        {
                            Messages.Message(
                                $"{pawn.LabelShort} has healed {injury.Label}!",
                                pawn,
                                MessageTypeDefOf.PositiveEvent);
                            MoteMaker.ThrowText(pawn.DrawPos, map, "Scar Healed!", Color.cyan, 3f);
                        }

                        return;
                    }
                    else if (Prefs.DevMode)
                    {
                        Log.Warning($"[Regen] Not enough resources to heal permanent injury");
                    }
                }
            }

            var allMissingParts = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .Select(h => h.Part)
                .Distinct()
                .ToList();

            if (allMissingParts.Count == 0)
            {
                pendingInstantRegen.Remove(pawnId);
                return;
            }

            if (ext.instantOrganRegeneration && ext.canRegenerateOrgans
                && !suppression.suppressOrgan)
            {
                var organ = allMissingParts.FirstOrDefault(part => CanRegenPart(part, ext, true));
                if (organ != null)
                {
                    if (Prefs.DevMode)
                        Log.Message($"[Regen] Found organ to regen: {organ.Label}");

                    pendingInstantRegen.Add(pawnId);

                    if (RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerOrganRegen))
                    {
                        RegenNaturalPart(pawn, organ, ext, "Organ Regenerated!", Color.cyan);
                        pendingInstantRegen.Remove(pawnId);
                        return;
                    }
                    else if (Prefs.DevMode)
                    {
                        Log.Warning($"[Regen] Not enough resources to regen organ");
                    }
                }
                else if (Prefs.DevMode)
                {
                    Log.Message($"[Regen] No organs found that can be regenerated");
                }
            }

            if (ext.instantLimbRegeneration && !suppression.suppressLimb)
            {
                var limb = allMissingParts.FirstOrDefault(part => CanRegenPart(part, ext, false));
                if (limb != null)
                {
                    if (Prefs.DevMode)
                        Log.Message($"[Regen] Found limb to regen: {limb.Label}");

                    pendingInstantRegen.Add(pawnId);

                    if (RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerLimbRegen))
                    {
                        RegenNaturalPart(pawn, limb, ext, "Limb Regenerated!", Color.green);
                        pendingInstantRegen.Remove(pawnId);
                    }
                    else if (Prefs.DevMode)
                    {
                        Log.Warning($"[Regen] Not enough resources to regen limb");
                    }
                }
            }
        }

        private bool CanRegenPart(BodyPartRecord part, RegenerationExtension ext, bool checkingForOrgan)
        {
            if (Prefs.DevMode)
                Log.Message($"[CanRegenPart] Checking {part.Label} ({part.def.defName}), checkingForOrgan={checkingForOrgan}");

            if (ext.fatalBodyParts.Any(fp => RegenerationHelper.IsPartType(part.def.defName, fp)))
            {
                if (Prefs.DevMode) Log.Message($"  -> BLOCKED: Fatal part");
                return false;
            }

            if (RegenerationHelper.IsPartType(part.def.defName, "Head") && !ext.canRegenerateHead)
            {
                if (Prefs.DevMode) Log.Message($"  -> BLOCKED: Head not allowed");
                return false;
            }

            bool isOrgan = RegenerationHelper.IsOrgan(part);
            if (Prefs.DevMode) Log.Message($"  -> IsOrgan: {isOrgan}");

            if (checkingForOrgan)
            {
                if (!isOrgan)
                {
                    if (Prefs.DevMode) Log.Message($"  -> BLOCKED: Not an organ");
                    return false;
                }

                if (!ext.canRegenerateOrgans)
                {
                    if (Prefs.DevMode) Log.Message($"  -> BLOCKED: canRegenerateOrgans = false");
                    return false;
                }

                if (RegenerationHelper.IsPartType(part.def.defName, "Brain") && !ext.canRegenerateBrain)
                {
                    if (Prefs.DevMode) Log.Message($"  -> BLOCKED: Brain regen disabled");
                    return false;
                }

                if (RegenerationHelper.IsPartType(part.def.defName, "Heart") && !ext.canRegenerateHeart)
                {
                    if (Prefs.DevMode) Log.Message($"  -> BLOCKED: Heart regen disabled");
                    return false;
                }

                if (Prefs.DevMode) Log.Message($"  -> ALLOWED: Can regen organ");
                return true;
            }

            bool result = !isOrgan;
            if (Prefs.DevMode)
                Log.Message($"  -> {(result ? "ALLOWED" : "BLOCKED")}: Limb check (not organ = {result})");
            return result;
        }

        private void RegenNaturalPart(Pawn pawn, BodyPartRecord part, RegenerationExtension ext,
            string text, Color color)
        {
            if (Prefs.DevMode)
                Log.Message($"[RegenNaturalPart] Starting regeneration of {part.Label}");

            var missingPartHediff = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missingPartHediff != null)
            {
                if (Prefs.DevMode) Log.Message($"[RegenNaturalPart] Removing missing part hediff");
                pawn.health.RemoveHediff(missingPartHediff);
            }

            var otherHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.Part == part && h != missingPartHediff)
                .ToList();

            foreach (var hediff in otherHediffs)
            {
                if (Prefs.DevMode) Log.Message($"[RegenNaturalPart] Removing hediff: {hediff.def.defName}");
                pawn.health.RemoveHediff(hediff);
            }

            pawn.health.RestorePart(part, null, true);

            Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            injury.Severity = 1f;
            pawn.health.AddHediff(injury, part, null, null);

            if (Prefs.DevMode) Log.Message($"[RegenNaturalPart] Part restored, added small cut");

            RefreshPawn(pawn);

            if (!ext.onlyNotifyMajorParts || RegenerationHelper.IsMajorPart(part))
            {
                var name = RegenerationHelper.GetBasePartName(part.def.defName);
                Messages.Message(
                    $"{pawn.LabelShort} has regenerated their natural {name}!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent);
                MoteMaker.ThrowText(pawn.DrawPos, map, text, color, 4f);
            }
        }

        private void TryStandUp(Pawn pawn)
        {
            if (!pawn.Downed || pawn.Dead) return;

            pawn.health.capacities.Notify_CapacityLevelsDirty();
            pawn.health.summaryHealth.Notify_HealthChanged();

            float consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
            float moving = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
            float blood = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);

            if (consciousness > 0.3f && moving > 0.2f && blood > 0.3f)
            {
                var major = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(i => i.Severity > 15f)
                    .ToList();

                var toRemove = new List<Hediff_Injury>();

                foreach (var inj in major)
                {
                    inj.Heal(inj.Severity * 0.5f);
                    if (inj.Severity < 1f)
                        toRemove.Add(inj);
                }

                foreach (var inj in toRemove)
                    pawn.health.RemoveHediff(inj);

                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();
            }
        }

        private void ShowResourceWarning(Pawn pawn, RegenerationExtension ext)
        {
            if (!ext.showResourceWarnings) return;

            int tick = Find.TickManager.TicksGame;
            int id = pawn.thingIDNumber;

            if (lastResourceWarn.ContainsKey(id) &&
                tick - lastResourceWarn[id] < ext.resourceWarningCooldownTicks)
                return;

            if (Rand.Chance(0.02f))
            {
                MoteMaker.ThrowText(pawn.DrawPos, map, $"Low {ext.resourceName}!", Color.yellow, 2f);
                lastResourceWarn[id] = tick;
            }
        }

        private void RefreshPawn(Pawn pawn)
        {
            foreach (var hediff in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                if (hediff.Bleeding && hediff.Part != null &&
                    !pawn.health.hediffSet.PartIsMissing(hediff.Part))
                {
                    if (hediff.Severity < 5f)
                        hediff.Tended(1.0f, 1.0f, 1);
                }
            }

            pawn.health.capacities.Notify_CapacityLevelsDirty();
            pawn.health.summaryHealth.Notify_HealthChanged();
            pawn.health.hediffSet.DirtyCache();

            if (pawn.Spawned)
            {
                pawn.Notify_ColorChanged();
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }

            PortraitsCache.SetDirty(pawn);
        }

        private void KillFromFatalDamage(Pawn pawn, RegenerationExtension ext)
        {
            var fatalPart = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(mp => ext.fatalBodyParts.Any(fp =>
                    RegenerationHelper.IsPartType(mp.Part.def.defName, fp)))
                .Select(mp => RegenerationHelper.GetBasePartName(mp.Part.def.defName).ToLower())
                .FirstOrDefault() ?? "vital part";

            Messages.Message(
                $"{pawn.LabelShort} dies from catastrophic {fatalPart} damage - even regeneration cannot save them.",
                pawn,
                MessageTypeDefOf.NegativeEvent);
            MoteMaker.ThrowText(pawn.DrawPos, map, $"Fatal {fatalPart} Damage!", Color.red, 5f);
            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 5);
            pawn.Kill(null);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(
                ref lastResourceWarn,
                "lastResourceWarn",
                LookMode.Value,
                LookMode.Value);

            Scribe_Collections.Look(
                ref pendingInstantRegen,
                "pendingInstantRegen",
                LookMode.Value);

            if (lastResourceWarn == null)
                lastResourceWarn = new Dictionary<int, int>();

            if (pendingInstantRegen == null)
                pendingInstantRegen = new HashSet<int>();
        }
    }
}