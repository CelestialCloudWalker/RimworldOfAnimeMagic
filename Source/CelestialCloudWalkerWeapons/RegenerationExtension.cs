using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

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

        public float scarHealChance = 0.2f;
        public int scarHealInterval = 1500;
    }

    public class MapComponent_BloodDemonRegeneration : MapComponent
    {
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
                    ProcessRegeneration(pawn, extension);
                    ProcessInstantLimbRegeneration(pawn, extension);
                    ProcessInstantOrganRegeneration(pawn, extension);
                }
            }
        }

        private bool HasBloodDemonArt(Pawn pawn)
        {
            return pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt", false)) ?? false;
        }

        private BloodDemonArtsGene GetBloodDemonGene(Pawn pawn)
        {
            return pawn?.genes?.GenesListForReading?.OfType<BloodDemonArtsGene>()?.FirstOrDefault();
        }

        private void ProcessRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            if (IsNeckDestroyed(pawn))
            {
                KillPawnFromNeckDestruction(pawn);
                return;
            }

            var injuries = pawn.health.hediffSet.hediffs
                .Where(h => h is Hediff_Injury injury && !injury.IsPermanent())
                .Cast<Hediff_Injury>()
                .ToList();

            foreach (var injury in injuries)
            {
                if (injury.Severity > 0)
                {
                    float healAmount = extension.healingPerTick * extension.healingMultiplier;
                    injury.Heal(healAmount);

                    if (Rand.Chance(0.05f))
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, map, "Regenerating...", Color.green, 2f);
                    }
                }
            }
        }

        private void ProcessInstantLimbRegeneration(Pawn pawn, RegenerationExtension extension)
        {
            if (!extension.instantLimbRegeneration) return;

            var missingPartHediffs = pawn.health.hediffSet.GetMissingPartsCommonAncestors()
                .Where(x => CanRegenerateLimb(x.Part))
                .ToList();

            foreach (var missingPartHediff in missingPartHediffs)
            {
                RegenerateLimb(pawn, missingPartHediff.Part);
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
                RegenerateOrgan(pawn, missingOrganHediff.Part);
                break;
            }
        }

        private bool IsNeckDestroyed(Pawn pawn)
        {
            var neck = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == "Neck");
            return neck != null && pawn.health.hediffSet.PartIsMissing(neck);
        }

        private bool CanRegenerateLimb(BodyPartRecord part)
        {
            string partName = part.def.defName;
            return partName != "Head" && partName != "Torso" && partName != "Neck"
                && !part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource)
                && !IsOrgan(part);
        }

        private bool CanRegenerateOrgan(BodyPartRecord part, RegenerationExtension extension)
        {
            if (!extension.canRegenerateOrgans || !IsOrgan(part)) return false;

            string partName = part.def.defName;

            if (partName == "Brain" && !extension.canRegenerateBrain) return false;
            if (partName == "Heart" && !extension.canRegenerateHeart) return false;
            if (partName == "Neck") return false;

            return true;
        }

        private bool IsOrgan(BodyPartRecord part)
        {
            return part.def.tags.Any(tag =>
                       tag == BodyPartTagDefOf.BloodPumpingSource ||
                       tag == BodyPartTagDefOf.BloodFiltrationSource ||
                       tag == BodyPartTagDefOf.BreathingSource ||
                       tag == BodyPartTagDefOf.EatingSource ||
                       tag == BodyPartTagDefOf.HearingSource ||
                       tag == BodyPartTagDefOf.SightSource ||
                       tag == BodyPartTagDefOf.ConsciousnessSource) ||
                   new[] { "Brain", "Heart", "Liver", "Kidney", "Lung", "Stomach" }.Contains(part.def.defName);
        }

        private void RegenerateLimb(Pawn pawn, BodyPartRecord part)
        {
            var missingPart = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missingPart != null)
            {
                pawn.health.RemoveHediff(missingPart);
            }

            var freshWound = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
            freshWound.Severity = 0.1f;
            pawn.health.AddHediff(freshWound);

            Messages.Message($"{pawn.LabelShort} has instantly regenerated their {part.LabelCap}!", pawn, MessageTypeDefOf.PositiveEvent);
            MoteMaker.ThrowText(pawn.DrawPos, map, "Limb Regenerated!", Color.green, 4f);
        }

        private void RegenerateOrgan(Pawn pawn, BodyPartRecord part)
        {
            var missingPart = pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>()
                .FirstOrDefault(h => h.Part == part);

            if (missingPart != null)
            {
                pawn.health.RemoveHediff(missingPart);
            }

            Messages.Message($"{pawn.LabelShort} has instantly regenerated their {part.LabelCap}!", pawn, MessageTypeDefOf.PositiveEvent);
            MoteMaker.ThrowText(pawn.DrawPos, map, "Organ Regenerated!", Color.magenta, 4f);

            if (part.def.defName == "Heart")
                MoteMaker.ThrowText(pawn.DrawPos, map, "Heart beats again!", Color.red, 5f);
            else if (part.def.defName == "Brain")
                MoteMaker.ThrowText(pawn.DrawPos, map, "Mind restored!", Color.blue, 5f);
        }

        private void KillPawnFromNeckDestruction(Pawn pawn)
        {
            Messages.Message($"{pawn.LabelShort} dies from catastrophic neck damage - even regeneration cannot save them.",
                pawn, MessageTypeDefOf.NegativeEvent);

            MoteMaker.ThrowText(pawn.DrawPos, map, "Fatal Neck Damage!", Color.red, 5f);
            FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 5);
            pawn.Kill(null);
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
                    pawn.health.RemoveHediff(injury);
                    if (pawn.Map != null)
                    {
                        string healedType = GetInjuryTypeName(injury);
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{healedType} healed", Color.green, 2f);

                        if (IsImportantInjury(injury))
                        {
                            Messages.Message($"{pawn.LabelShort}'s {healedType} has been completely healed by regeneration!",
                                pawn, MessageTypeDefOf.PositiveEvent);
                        }
                    }
                    break;
                }
            }
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
