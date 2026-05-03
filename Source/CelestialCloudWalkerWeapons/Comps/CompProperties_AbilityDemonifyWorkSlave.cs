using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDemonifyWorkSlave : CompProperties_AbilityEffect
    {
        public float successChance = 0.70f;
        public float nearDeathHealthThreshold = 0.25f;
        public float nearDeathSuccessBonus = 0.20f;
        public HediffDef failureHediff;
        public float failureHediffSeverity = 1.0f;
        public bool requiresPrisoner = false;
        public bool requiresDowned = false;
        public float aoeRadius = 0f;
        public int maxTargets = 1;
        public string convertedXenotypeName = "";
        public float resourceStartPercent = 0.50f;
        public bool removeExistingEndogenes = false;
        public List<HediffDef> giveHediffsOnSuccess = new List<HediffDef>();
        public bool convertOnly = false;
        public bool joinColony = false;
        public FactionDef slaveFaction = null;
        public string successMessage = "{0} has been transformed into a demon slave.";
        public string failureMessage = "{0} resisted the demonification.";
        public string invalidTargetMessage = "Target is not eligible for demonification.";

        public CompProperties_AbilityDemonifyWorkSlave()
        {
            compClass = typeof(CompAbility_DemonifyWorkSlave);
        }
    }

    public class CompAbility_DemonifyWorkSlave : CompAbilityEffect
    {
        public new CompProperties_AbilityDemonifyWorkSlave Props =>
            (CompProperties_AbilityDemonifyWorkSlave)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            List<Pawn> targets = new List<Pawn>();

            Pawn primary = target.Pawn;
            if (primary != null && IsValidTarget(primary))
                targets.Add(primary);

            if (Props.aoeRadius > 0f && Props.maxTargets > 1)
            {
                foreach (Pawn nearby in caster.Map.mapPawns.AllPawnsSpawned
                    .Where(p => p != primary
                        && p != caster
                        && !p.Dead
                        && p.Position.DistanceTo(target.Cell) <= Props.aoeRadius
                        && IsValidTarget(p))
                    .OrderBy(p => p.Position.DistanceTo(target.Cell)))
                {
                    if (targets.Count >= Props.maxTargets) break;
                    targets.Add(nearby);
                }
            }

            if (targets.Count == 0)
            {
                Messages.Message(
                    string.Format(Props.invalidTargetMessage,
                        target.Pawn?.LabelShort ?? "Target"),
                    target.ToTargetInfo(caster.Map),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            foreach (Pawn pawn in targets)
                AttemptConversion(pawn, caster);
        }

        private bool IsValidTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;
            if (Props.requiresPrisoner && !pawn.IsPrisonerOfColony) return false;
            if (Props.requiresDowned && !pawn.Downed) return false;
            return true;
        }

        private void AttemptConversion(Pawn pawn, Pawn caster)
        {
            float chance = Props.successChance;

            if (Props.nearDeathHealthThreshold > 0f && Props.nearDeathSuccessBonus > 0f)
            {
                if (pawn.health.summaryHealth.SummaryHealthPercent
                        <= Props.nearDeathHealthThreshold)
                    chance += Props.nearDeathSuccessBonus;
            }

            chance = Mathf.Clamp01(chance);

            if (!Rand.Chance(chance))
            {
                if (Props.failureHediff != null)
                {
                    Hediff fh = HediffMaker.MakeHediff(Props.failureHediff, pawn);
                    fh.Severity = Props.failureHediffSeverity;
                    pawn.health.AddHediff(fh);
                }

                if (!Props.failureMessage.NullOrEmpty())
                    Messages.Message(
                        string.Format(Props.failureMessage, pawn.LabelShort),
                        pawn, MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            if (Props.removeExistingEndogenes && pawn.genes != null)
            {
                foreach (Gene g in pawn.genes.GenesListForReading
                    .Where(g => g.def.endogeneCategory != EndogeneCategory.None)
                    .ToList())
                    pawn.genes.RemoveGene(g);
            }

            if (!Props.convertedXenotypeName.NullOrEmpty())
            {
                XenotypeDef xenotype = DefDatabase<XenotypeDef>
                    .GetNamedSilentFail(Props.convertedXenotypeName);

                if (xenotype != null && pawn.genes != null)
                {
                    foreach (Gene g in pawn.genes.GenesListForReading.ToList())
                        pawn.genes.RemoveGene(g);
                    foreach (GeneDef gd in xenotype.genes)
                        pawn.genes.AddGene(gd, false);
                }
                else if (xenotype == null)
                {
                    Log.Warning($"[AnimeArsenal] DemonifyWorkSlave: xenotype " +
                                $"'{Props.convertedXenotypeName}' not found.");
                }
            }

            if (pawn.genes != null)
            {
                GeneDef bloodDemonGeneDef = DefDatabase<GeneDef>.GetNamed("BloodDemonArt");
                if (!pawn.genes.HasActiveGene(bloodDemonGeneDef))
                {
                    Gene gene = pawn.genes.AddGene(bloodDemonGeneDef, true);
                    if (gene is EMF.Gene_BasicResource resourceGene)
                    {
                        resourceGene.EnableResource = true;
                        resourceGene.Value = resourceGene.Max * Props.resourceStartPercent;
                        resourceGene.ResetRegenTicks();
                    }
                }
            }

            if (!Props.convertOnly)
            {
                HediffDef slaveHediffDef = DefDatabase<HediffDef>.GetNamed("DemonWorkSlaveHediff");
                Hediff slaveHediff = HediffMaker.MakeHediff(slaveHediffDef, pawn);
                pawn.health.AddHediff(slaveHediff);

                HediffComp_DemonWorkSlaveEffect slaveComp =
                    slaveHediff.TryGetComp<HediffComp_DemonWorkSlaveEffect>();
                slaveComp?.SetSlaveMaster(caster);
            }

            if (!Props.giveHediffsOnSuccess.NullOrEmpty())
            {
                foreach (HediffDef hd in Props.giveHediffsOnSuccess)
                    pawn.health.AddHediff(HediffMaker.MakeHediff(hd, pawn));
            }

            if (Props.joinColony)
            {
                pawn.SetFaction(Faction.OfPlayer);
                pawn.guest?.SetGuestStatus(null);
            }
            else if (Props.slaveFaction != null)
            {
                Faction faction = Find.FactionManager
                    .FirstFactionOfDef(Props.slaveFaction);
                if (faction != null)
                    pawn.SetFaction(faction);
            }
            else if (!Props.convertOnly)
            {
                pawn.SetFaction(caster.Faction);
                pawn.guest?.SetGuestStatus(null);
            }

            if (!Props.successMessage.NullOrEmpty())
                Messages.Message(
                    string.Format(Props.successMessage, pawn.LabelShort),
                    pawn, MessageTypeDefOf.PositiveEvent, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null) return false;

            if (!IsValidTarget(pawn))
            {
                if (throwMessages)
                    Messages.Message(
                        string.Format(Props.invalidTargetMessage, pawn.LabelShort),
                        pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }
}