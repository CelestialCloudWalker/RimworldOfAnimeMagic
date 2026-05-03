using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_CloneSummon : CompProperties_AbilityEffect
    {
        public bool copyName = true;
        public bool copyApparel = true;
        public bool copySkills = true;
        public bool copyTraits = true;
        public bool copyGenes = true;
        public bool copyAbilities = true;
        public float healthMultiplier = 1.0f;
        public List<string> blockedAbilities = new List<string>();
        public string summonKindPrefix;
        public int maxSummons = -1;
        public HediffDef lifespanHediff;

        public List<HediffDef> bonusHediffs = new List<HediffDef>();

        public PawnKindDef spawnKind;
        public int spawnCount = 0;
        public float spawnRadius = 3f;
        public string cloneNickname;

        public HediffDef casterShrinkHediff;
        public List<HediffDef> casterHediffsToSwapOut = new List<HediffDef>();

        public CompProperties_CloneSummon()
        {
            compClass = typeof(CompAbilityEffect_CloneSummon);
        }
    }

    public class CompAbilityEffect_CloneSummon : CompAbilityEffect
    {
        public new CompProperties_CloneSummon Props => (CompProperties_CloneSummon)props;

        private bool pendingCopy = false;
        private Pawn savedCaster = null;
        private List<Pawn> pendingClones = new List<Pawn>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            Map map = caster.Map;
            pendingClones.Clear();

            if (Props.maxSummons > 0 && Props.summonKindPrefix != null)
            {
                int existing = map.mapPawns.AllPawnsSpawned
                    .Count(p => p.Faction == caster.Faction
                        && p.kindDef?.defName.StartsWith(Props.summonKindPrefix) == true
                        && !p.Dead);

                if (existing >= Props.maxSummons) return;
            }

            if (Props.spawnKind != null && Props.spawnCount > 0)
            {
                List<IntVec3> spawnCells = GenRadial
                    .RadialCellsAround(caster.Position, Props.spawnRadius, true)
                    .Where(c => c != caster.Position
                        && c.InBounds(map)
                        && c.Walkable(map)
                        && c.GetFirstPawn(map) == null)
                    .InRandomOrder()
                    .Take(Props.spawnCount)
                    .ToList();

                foreach (IntVec3 spawnCell in spawnCells)
                {
                    Pawn clone = PawnGenerator.GeneratePawn(Props.spawnKind, caster.Faction);
                    GenSpawn.Spawn(clone, spawnCell, map);
                    pendingClones.Add(clone);
                }
            }

            if (Props.casterShrinkHediff != null && caster.health?.hediffSet != null)
            {
                if (Props.casterHediffsToSwapOut != null)
                {
                    foreach (HediffDef swapOut in Props.casterHediffsToSwapOut)
                    {
                        if (swapOut == null) continue;
                        Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(swapOut);
                        if (existing != null)
                            caster.health.RemoveHediff(existing);
                    }
                }

                if (!caster.health.hediffSet.HasHediff(Props.casterShrinkHediff))
                {
                    Hediff shrink = HediffMaker.MakeHediff(Props.casterShrinkHediff, caster);
                    caster.health.AddHediff(shrink);
                }
            }

            savedCaster = caster;
            pendingCopy = true;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!pendingCopy) return;
            if (savedCaster == null || savedCaster.Map == null)
            {
                pendingCopy = false;
                return;
            }

            pendingCopy = false;

            if (pendingClones.Count == 0 && Props.summonKindPrefix != null)
            {
                Pawn emfClone = savedCaster.Map.mapPawns.AllPawnsSpawned
                    .Where(p => p != savedCaster
                        && p.Faction == savedCaster.Faction
                        && p.kindDef.defName.StartsWith(Props.summonKindPrefix))
                    .OrderByDescending(p => p.thingIDNumber)
                    .FirstOrDefault();

                if (emfClone != null)
                    pendingClones.Add(emfClone);
                else
                    Log.Warning($"[AnimeArsenal] CloneSummon: no clone found with prefix '{Props.summonKindPrefix}'.");
            }

            foreach (Pawn clone in pendingClones)
            {
                if (clone == null || clone.Destroyed || clone.Dead) continue;
                CopyPawnToClone(savedCaster, clone);
            }

            pendingClones.Clear();
            savedCaster = null;
        }

        private void CopyPawnToClone(Pawn source, Pawn clone)
        {
            if (!string.IsNullOrEmpty(Props.cloneNickname))
            {
                if (Props.copyName && source.Name is NameTriple sourceName)
                    clone.Name = new NameTriple(sourceName.First, Props.cloneNickname, sourceName.Last);
                else
                    clone.Name = new NameTriple("", Props.cloneNickname, "");
            }
            else if (Props.copyName && source.Name is NameTriple sn)
            {
                clone.Name = new NameTriple(sn.First, sn.Nick + " (Clone)", sn.Last);
            }

            if (Props.copySkills && source.skills != null && clone.skills != null)
            {
                foreach (SkillRecord sourceSkill in source.skills.skills)
                {
                    SkillRecord cloneSkill = clone.skills.GetSkill(sourceSkill.def);
                    if (cloneSkill == null) continue;
                    cloneSkill.levelInt = sourceSkill.levelInt;
                    cloneSkill.passion = sourceSkill.passion;
                    cloneSkill.xpSinceLastLevel = sourceSkill.xpSinceLastLevel;
                    cloneSkill.xpSinceMidnight = sourceSkill.xpSinceMidnight;
                }
            }

            if (Props.copyTraits && source.story?.traits != null && clone.story?.traits != null)
            {
                clone.story.traits.allTraits.Clear();
                foreach (Trait trait in source.story.traits.allTraits)
                    clone.story.traits.allTraits.Add(new Trait(trait.def, trait.Degree));
            }

            if (source.story != null && clone.story != null)
            {
                clone.story.bodyType = source.story.bodyType;
                clone.story.hairDef = source.story.hairDef;
                clone.story.HairColor = source.story.HairColor;
                clone.story.headType = source.story.headType;
                clone.story.furDef = source.story.furDef;
                clone.story.SkinColorBase = source.story.SkinColorBase;
                clone.story.skinColorOverride = source.story.skinColorOverride;
            }

            if (Props.copyGenes && source.genes != null && clone.genes != null)
            {
                foreach (Gene gene in clone.genes.GenesListForReading.ToList())
                    clone.genes.RemoveGene(gene);

                foreach (Gene sourceGene in source.genes.GenesListForReading)
                {
                    if (sourceGene.Overridden) continue;
                    bool isEndo = sourceGene.def.endogeneCategory != EndogeneCategory.None;
                    clone.genes.AddGene(sourceGene.def, isEndo);
                }
            }

            if (Props.copyAbilities && source.abilities != null && clone.abilities != null)
            {
                foreach (Ability ab in clone.abilities.abilities.ToList())
                    clone.abilities.RemoveAbility(ab.def);

                foreach (Ability ab in source.abilities.abilities)
                {
                    if (Props.blockedAbilities.Contains(ab.def.defName)) continue;
                    clone.abilities.GainAbility(ab.def);
                }
            }

            if (Props.copyApparel && source.apparel != null && clone.apparel != null)
            {
                foreach (Apparel ap in clone.apparel.WornApparel.ToList())
                {
                    clone.apparel.Remove(ap);
                    ap.Destroy();
                }

                foreach (Apparel sourceAp in source.apparel.WornApparel)
                {
                    Apparel newAp = (Apparel)ThingMaker.MakeThing(sourceAp.def, sourceAp.Stuff);

                    if (sourceAp.TryGetComp<CompColorable>() is CompColorable srcColor
                        && newAp.TryGetComp<CompColorable>() is CompColorable dstColor)
                        dstColor.SetColor(srcColor.Color);

                    if (sourceAp.TryGetComp<CompQuality>() is CompQuality srcQual
                        && newAp.TryGetComp<CompQuality>() is CompQuality dstQual)
                        dstQual.SetQuality(srcQual.Quality, ArtGenerationContext.Colony);

                    clone.apparel.Wear(newAp, false);
                }
            }

            if (Props.healthMultiplier != 1.0f && clone.health?.hediffSet != null)
            {
                foreach (Hediff hediff in clone.health.hediffSet.hediffs.ToList())
                {
                    if (hediff is Hediff_Injury)
                        clone.health.RemoveHediff(hediff);
                }
            }

            if (Props.lifespanHediff != null)
            {
                Hediff lifespan = HediffMaker.MakeHediff(Props.lifespanHediff, clone);
                clone.health.AddHediff(lifespan);
            }

            if (Props.bonusHediffs != null)
            {
                foreach (HediffDef bonusDef in Props.bonusHediffs)
                {
                    if (bonusDef == null) continue;
                    if (clone.health.hediffSet.HasHediff(bonusDef)) continue;
                    Hediff bonus = HediffMaker.MakeHediff(bonusDef, clone);
                    clone.health.AddHediff(bonus);
                }
            }

            PortraitsCache.SetDirty(clone);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) => true;
    }

    public class CompProperties_AbilityApplyLifespan : CompProperties_AbilityEffect
    {
        public HediffDef lifespanHediff;
        public PawnKindDef targetKind;

        public CompProperties_AbilityApplyLifespan()
        {
            compClass = typeof(CompAbilityEffect_ApplyLifespan);
        }
    }

    public class CompAbilityEffect_ApplyLifespan : CompAbilityEffect
    {
        public new CompProperties_AbilityApplyLifespan Props =>
            (CompProperties_AbilityApplyLifespan)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            foreach (Pawn p in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == caster) continue;
                if (p.Faction != caster.Faction) continue;
                if (Props.targetKind != null && p.kindDef != Props.targetKind) continue;
                if (p.health.hediffSet.HasHediff(Props.lifespanHediff)) continue;
                if (p.Position.DistanceTo(caster.Position) > 4f) continue;

                Hediff lifespan = HediffMaker.MakeHediff(Props.lifespanHediff, p);
                p.health.AddHediff(lifespan);
            }
        }
    }
}