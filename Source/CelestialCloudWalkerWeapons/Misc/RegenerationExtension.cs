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

        public static bool IsOrgan(BodyPartRecord part)
        {
            var baseName = GetBasePartName(part.def.defName).ToLower();
            return OrganTags.Contains(baseName) ||
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
            return PermanentKeywords.Any(keyword => defName.Contains(keyword)) ||
                   (!hediff.def.tendable && !hediff.def.everCurableByItem && hediff.Severity > 0);
        }

        public static bool CanConsumeResource(Pawn pawn, RegenerationExtension ext, float cost)
        {
            if (!ext.consumeResourcesOnRegeneration) return true;
            var gene = GetResourceGene(pawn);
            return gene?.Value >= (cost + ext.minimumResourcesRequired);
        }

        public static bool TryConsumeResource(Pawn pawn, RegenerationExtension ext, float cost)
        {
            if (!ext.consumeResourcesOnRegeneration) return true;
            var gene = GetResourceGene(pawn);
            if (gene?.Value >= (cost + ext.minimumResourcesRequired))
            {
                gene.Consume(cost);
                return true;
            }
            return false;
        }

        private static Gene_BasicResource GetResourceGene(Pawn pawn)
        {
            return pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g.Active && g.def.GetModExtension<RegenerationExtension>() != null)
                as Gene_BasicResource;
        }
    }

    public class MapComponent_BloodDemonRegeneration : MapComponent
    {
        private Dictionary<int, int> lastResourceWarn = new Dictionary<int, int>();

        public MapComponent_BloodDemonRegeneration(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => HasRegenGene(p) && !p.Dead))
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

            ProcessNormalRegen(pawn, ext);
            ProcessInstantRegen(pawn, ext);
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
                .Any(mp => ext.fatalBodyParts.Any(fp => RegenerationHelper.IsPartType(mp.Part.def.defName, fp)) ||
                          (!ext.canRegenerateHead && RegenerationHelper.IsPartType(mp.Part.def.defName, "Head")));
        }

        private void ProcessNormalRegen(Pawn pawn, RegenerationExtension ext)
        {
            var injuries = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(i => !i.IsPermanent() && i.Severity > 0).ToList();

            bool blocked = false;
            foreach (var injury in injuries)
            {
                float heal = ext.healingPerTick * ext.healingMultiplier;

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
                    MoteMaker.ThrowText(pawn.DrawPos, map, "Regenerating...", Color.green, 2f);
            }

            if (blocked) ShowResourceWarning(pawn, ext);
        }

        private void ProcessInstantRegen(Pawn pawn, RegenerationExtension ext)
        {
            var missing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().ToList();

            if (ext.instantLimbRegeneration)
            {
                var limb = missing.FirstOrDefault(x => CanRegenPart(x.Part, ext, false));
                if (limb != null && RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerLimbRegen))
                    RegenPart(pawn, limb.Part, ext, "Limb Regenerated!", Color.green);
            }

            if (ext.instantOrganRegeneration)
            {
                var organ = missing.FirstOrDefault(x => CanRegenPart(x.Part, ext, true));
                if (organ != null && RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerOrganRegen))
                    RegenPart(pawn, organ.Part, ext, "Organ Regenerated!", Color.cyan);
            }
        }

        private bool CanRegenPart(BodyPartRecord part, RegenerationExtension ext, bool isOrgan)
        {
            if (ext.fatalBodyParts.Any(fp => RegenerationHelper.IsPartType(part.def.defName, fp))) return false;
            if (RegenerationHelper.IsPartType(part.def.defName, "Head") && !ext.canRegenerateHead) return false;

            if (isOrgan)
            {
                if (!ext.canRegenerateOrgans || !RegenerationHelper.IsOrgan(part)) return false;
                if (RegenerationHelper.IsPartType(part.def.defName, "Brain") && !ext.canRegenerateBrain) return false;
                if (RegenerationHelper.IsPartType(part.def.defName, "Heart") && !ext.canRegenerateHeart) return false;
                return true;
            }

            return !RegenerationHelper.IsOrgan(part);
        }

        private void RegenPart(Pawn pawn, BodyPartRecord part, RegenerationExtension ext, string text, Color color)
        {
            var missing = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missing != null)
            {
                pawn.health.RemoveHediff(missing);
                var cut = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
                cut.Severity = 0.01f;
                pawn.health.AddHediff(cut);
            }

            RefreshPawn(pawn);

            if (!ext.onlyNotifyMajorParts || RegenerationHelper.IsMajorPart(part))
            {
                var name = RegenerationHelper.GetBasePartName(part.def.defName);
                Messages.Message($"{pawn.LabelShort} has instantly regenerated their {name}!",
                    pawn, MessageTypeDefOf.PositiveEvent);
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
                var major = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                    .Where(i => i.Severity > 15f).ToList();

                foreach (var injury in major)
                {
                    injury.Heal(injury.Severity * 0.5f);
                    if (injury.Severity < 1f) pawn.health.RemoveHediff(injury);
                }

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
            pawn.health.capacities.Notify_CapacityLevelsDirty();
            pawn.health.summaryHealth.Notify_HealthChanged();
            if (pawn.Spawned) pawn.Notify_ColorChanged();
            PortraitsCache.SetDirty(pawn);
        }

        private void KillFromFatalDamage(Pawn pawn, RegenerationExtension ext)
        {
            var fatalPart = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(mp => ext.fatalBodyParts.Any(fp => RegenerationHelper.IsPartType(mp.Part.def.defName, fp)))
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
            Scribe_Collections.Look(ref lastResourceWarn, "lastResourceWarn", LookMode.Value, LookMode.Value);

            if (lastResourceWarn == null)
                lastResourceWarn = new Dictionary<int, int>();
        }
    }

    public class BloodDemonRegenerationGene : Gene
    {
        private int lastScarCheck = 0;

        public override void Tick()
        {
            base.Tick();

            var ext = def.GetModExtension<RegenerationExtension>();
            if (ext == null) return;

            if (Find.TickManager.TicksGame - lastScarCheck >= ext.scarHealInterval)
            {
                lastScarCheck = Find.TickManager.TicksGame;
                ProcessScarHealing(ext);
            }
        }

        private void ProcessScarHealing(RegenerationExtension ext)
        {
            var scar = pawn.health.hediffSet.hediffs.FirstOrDefault(RegenerationHelper.IsPermanentInjury);

            if (scar != null && Rand.Chance(ext.scarHealChance) &&
                RegenerationHelper.TryConsumeResource(pawn, ext, ext.resourceCostPerScarHeal))
            {
                pawn.health.RemoveHediff(scar);

                if (pawn.Map != null)
                {
                    var type = scar.def.label ?? "Permanent Injury";
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{type} healed", Color.blue, 2f);

                    var name = scar.def.defName.ToLower();
                    if (name.Contains("blindness") || name.Contains("hearing") || name.Contains("dementia"))
                    {
                        Messages.Message($"{pawn.LabelShort}'s {type} has been completely healed by regeneration!",
                            pawn, MessageTypeDefOf.PositiveEvent);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastScarCheck, "lastScarCheck", 0);
        }
    }
}