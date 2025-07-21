using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    // Custom CompProperties for configurable damage
    public class CompProperties_AbilityStealBite : CompProperties_AbilityEffect
    {
        public DamageDef damageType = DamageDefOf.Bite;
        public float baseDamage = 8f;
        public float armorPenetration = 0f;
        public bool scaleDamageWithMelee = true;

        public CompProperties_AbilityStealBite()
        {
            compClass = typeof(CompAbilityStealBite);
        }
    }

    // Custom ability component that handles the copying logic
    public class CompAbilityStealBite : CompAbilityEffect
    {
        private new CompProperties_AbilityStealBite Props => (CompProperties_AbilityStealBite)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn == null || parent.pawn == null)
                return;

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;

            // Basic target validation (just make sure it's not null and alive)
            if (victim.Dead)
            {
                Messages.Message("AbilityStealBite_TargetDead".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // Debug logging
            Log.Message($"Attempting to copy ability from {victim.LabelShort}");
            Log.Message($"Victim abilities tracker exists: {victim.abilities != null}");
            Log.Message($"Victim abilities list exists: {victim.abilities?.abilities != null}");
            Log.Message($"Victim abilities count: {victim.abilities?.abilities?.Count ?? 0}");

            // Get random ability from target
            AbilityDef copiedAbility = GetRandomAbility(victim);

            if (copiedAbility == null)
            {
                Messages.Message("AbilityStealBite_NoAbilities".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Log.Message($"Selected ability to copy: {copiedAbility.defName}");

            // Check success chance
            float successChance = caster.GetStatValue(DefDatabase<StatDef>.GetNamed("AbilityStealChance"));
            if (Rand.Value > successChance)
            {
                Messages.Message("AbilityStealBite_Failed".Translate(caster.LabelShort), MessageTypeDefOf.NegativeEvent);
                return;
            }

            // Copy ability to caster (don't remove from victim)
            AddAbilityToPawn(caster, copiedAbility);

            // Deal bite damage to victim
            DealBiteDamage(caster, victim);

            // Add thoughts with proper null checking
            TryAddThoughtSafely(caster, "CopiedAbility");
            TryAddThoughtSafely(victim, "AbilityCopied");

            // Visual effects
            FleckMaker.ThrowDustPuffThick(victim.Position.ToVector3(), victim.Map, 2f, Color.red);
            FleckMaker.ThrowDustPuffThick(caster.Position.ToVector3(), caster.Map, 2f, Color.green);

            Messages.Message("AbilityStealBite_Success".Translate(caster.LabelShort, copiedAbility.LabelCap),
                MessageTypeDefOf.PositiveEvent);
        }

        private void DealBiteDamage(Pawn caster, Pawn victim)
        {
            try
            {
                // Calculate damage based on XML configuration
                float baseDamage = Props.baseDamage;
                float finalDamage = baseDamage;

                // Scale with melee damage if enabled
                if (Props.scaleDamageWithMelee)
                {
                    float meleeDamageMultiplier = caster.GetStatValue(StatDefOf.MeleeDamageFactor);
                    finalDamage *= meleeDamageMultiplier;
                }

                int damage = Mathf.RoundToInt(finalDamage);

                // Create damage info - try the most basic constructor first
                var biteInfo = new DamageInfo(Props.damageType, damage);
                biteInfo.SetInstantPermanentInjury(false);

                // Try to set additional properties if they exist
                try
                {
                    biteInfo.SetIgnoreArmor(false);
                    biteInfo.SetIgnoreInstantKillProtection(false);
                }
                catch
                {
                    // If these methods don't exist, just continue
                }

                // Apply damage
                victim.TakeDamage(biteInfo);

                Log.Message($"{caster.LabelShort} bit {victim.LabelShort} for {damage} {Props.damageType.label} damage");

                // Skip battle log entry - it's often problematic and not essential
                // The damage will still show up in other ways
            }
            catch (Exception ex)
            {
                Log.Error($"Error dealing bite damage: {ex.Message}");
            }
        }

        private void TryAddThoughtSafely(Pawn pawn, string thoughtDefName)
        {
            try
            {
                // Check if pawn can have thoughts
                if (pawn?.needs?.mood?.thoughts?.memories == null)
                {
                    Log.Warning($"Cannot add thought to {pawn?.LabelShort ?? "null pawn"} - missing mood/thoughts system");
                    return;
                }

                // Get the thought definition
                var thoughtDef = DefDatabase<ThoughtDef>.GetNamed(thoughtDefName);
                if (thoughtDef == null)
                {
                    Log.Error($"ThoughtDef '{thoughtDefName}' not found!");
                    return;
                }

                // Validate thought definition has stages
                if (thoughtDef.stages == null || thoughtDef.stages.Count == 0)
                {
                    Log.Error($"ThoughtDef '{thoughtDefName}' has no stages defined!");
                    return;
                }

                // Add the thought
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                Log.Message($"Successfully added thought '{thoughtDefName}' to {pawn.LabelShort}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding thought '{thoughtDefName}' to {pawn?.LabelShort ?? "null pawn"}: {ex.Message}");
            }
        }

        private AbilityDef GetRandomAbility(Pawn pawn)
        {
            // Check if pawn has abilities tracker
            if (pawn.abilities == null)
            {
                Log.Warning($"Pawn {pawn.LabelShort} has no abilities tracker");
                return null;
            }

            if (pawn.abilities.abilities == null || pawn.abilities.abilities.Count == 0)
            {
                Log.Warning($"Pawn {pawn.LabelShort} has no abilities in their abilities list");
                return null;
            }

            // Get all abilities - be very inclusive to catch modded abilities
            var availableAbilities = new List<AbilityDef>();

            // Create a copy of the abilities list to avoid modification during enumeration
            var abilitiesCopy = new List<Ability>(pawn.abilities.abilities);

            for (int i = 0; i < abilitiesCopy.Count; i++)
            {
                var ability = abilitiesCopy[i];
                if (ability?.def != null)
                {
                    availableAbilities.Add(ability.def);
                }
            }

            Log.Message($"Found {availableAbilities.Count} abilities on {pawn.LabelShort}:");
            foreach (var ability in availableAbilities)
            {
                Log.Message($"  - {ability.defName} ({ability.LabelCap}) - Mod: {ability.modContentPack?.Name ?? "Core"}");
            }

            if (availableAbilities.Count == 0)
            {
                Log.Warning($"No valid abilities found on {pawn.LabelShort}");
                return null;
            }

            var selectedAbility = availableAbilities.RandomElement();
            Log.Message($"Selected ability: {selectedAbility.defName}");

            return selectedAbility;
        }

        private void AddAbilityToPawn(Pawn pawn, AbilityDef ability)
        {
            if (pawn.abilities == null)
                pawn.abilities = new Pawn_AbilityTracker(pawn);

            Log.Message($"Adding ability {ability.defName} to {pawn.LabelShort}");

            try
            {
                // Check if pawn already has this ability
                bool hasAbility = pawn.abilities.abilities.Any(a => a?.def == ability);
                if (!hasAbility)
                {
                    pawn.abilities.GainAbility(ability);
                    Log.Message($"Successfully added ability {ability.defName}");
                }
                else
                {
                    Log.Message($"Pawn already has ability {ability.defName}, skipping");
                }

                // Add copied ability hediff to track it
                var hediffDef = DefDatabase<HediffDef>.GetNamed("CopiedAbilityHediff");
                if (hediffDef != null)
                {
                    var hediff = (HediffCopiedAbility)HediffMaker.MakeHediff(hediffDef, pawn);
                    hediff.copiedAbility = ability;
                    pawn.health.AddHediff(hediff);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error adding ability {ability.defName} to {pawn.LabelShort}: {ex.Message}");
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;

            if (target.Pawn == null)
            {
                if (throwMessages)
                    Messages.Message("AbilityStealBite_MustTargetPawn".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            if (target.Pawn.Dead)
            {
                if (throwMessages)
                    Messages.Message("AbilityStealBite_TargetDead".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }
    }

    // Custom hediff class to track copied abilities
    public class HediffCopiedAbility : Hediff
    {
        public AbilityDef copiedAbility;

        public override string LabelInBrackets => copiedAbility?.LabelCap ?? "unknown";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref copiedAbility, "copiedAbility");
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            // Remove the copied ability when hediff is removed
            if (copiedAbility != null && pawn?.abilities?.abilities != null)
            {
                var abilityToRemove = pawn.abilities.abilities.FirstOrDefault(a => a.def == copiedAbility);
                if (abilityToRemove != null)
                {
                    pawn.abilities.abilities.Remove(abilityToRemove);
                }
            }
        }
    }
}