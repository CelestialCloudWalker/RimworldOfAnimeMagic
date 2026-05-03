using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CloneSummonConfig
    {
        public PawnKindDef spawnKind;
        public int spawnCount = 1;
        public float spawnRadius = 3f;
        public string cloneNickname;

        public bool copyName = false;
        public bool copyApparel = true;
        public bool copySkills = true;
        public bool copyTraits = false;
        public bool copyGenes = true;
        public bool copyAbilities = false;

        public float healthMultiplier = 1f;
        public HediffDef lifespanHediff;

        public List<HediffDef> bonusHediffs = new List<HediffDef>();
        public List<string> blockedAbilities = new List<string>();
        public HediffDef casterShrinkHediff;
        public List<HediffDef> casterHediffsToSwapOut = new List<HediffDef>();
    }

    public class CompProperties_MultiCloneSummon : CompProperties_AbilityEffect
    {
        public List<CloneSummonConfig> clones = new List<CloneSummonConfig>();

        public CompProperties_MultiCloneSummon()
        {
            compClass = typeof(CompAbilityEffect_MultiCloneSummon);
        }
    }

    public class CompAbilityEffect_MultiCloneSummon : CompAbilityEffect
    {
        public new CompProperties_MultiCloneSummon Props =>
            (CompProperties_MultiCloneSummon)props;

        private bool pendingCopy = false;
        private Pawn savedCaster = null;
        private List<(CloneSummonConfig cfg, Pawn clone)> pendingWork =
            new List<(CloneSummonConfig, Pawn)>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            Map map = caster.Map;
            pendingWork.Clear();

            foreach (CloneSummonConfig cfg in Props.clones)
            {
                if (cfg.spawnKind == null || cfg.spawnCount <= 0) continue;

                List<IntVec3> cells = GenRadial
                    .RadialCellsAround(caster.Position, cfg.spawnRadius, true)
                    .Where(c => c != caster.Position
                             && c.InBounds(map)
                             && c.Walkable(map)
                             && c.GetFirstPawn(map) == null)
                    .InRandomOrder()
                    .Take(cfg.spawnCount)
                    .ToList();

                foreach (IntVec3 cell in cells)
                {
                    Pawn clone = PawnGenerator.GeneratePawn(cfg.spawnKind, caster.Faction);
                    GenSpawn.Spawn(clone, cell, map);
                    pendingWork.Add((cfg, clone));
                }

                if (cfg.casterShrinkHediff != null && caster.health?.hediffSet != null)
                {
                    foreach (HediffDef swapOut in cfg.casterHediffsToSwapOut)
                    {
                        Hediff h = caster.health.hediffSet.GetFirstHediffOfDef(swapOut);
                        if (h != null) caster.health.RemoveHediff(h);
                    }

                    if (!caster.health.hediffSet.HasHediff(cfg.casterShrinkHediff))
                        caster.health.AddHediff(
                            HediffMaker.MakeHediff(cfg.casterShrinkHediff, caster));
                }
            }

            savedCaster = caster;
            pendingCopy = true;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!pendingCopy) return;

            pendingCopy = false;

            if (savedCaster == null || savedCaster.Map == null)
            {
                pendingWork.Clear();
                savedCaster = null;
                return;
            }

            foreach ((CloneSummonConfig cfg, Pawn clone) in pendingWork)
            {
                if (clone == null || clone.Destroyed || clone.Dead) continue;
                CopyToClone(savedCaster, clone, cfg);
            }

            pendingWork.Clear();
            savedCaster = null;
        }

        private void CopyToClone(Pawn source, Pawn clone, CloneSummonConfig cfg)
        {
            if (!string.IsNullOrEmpty(cfg.cloneNickname))
            {
                if (cfg.copyName && source.Name is NameTriple sn)
                    clone.Name = new NameTriple(sn.First, cfg.cloneNickname, sn.Last);
                else
                    clone.Name = new NameTriple("", cfg.cloneNickname, "");
            }
            else if (cfg.copyName && source.Name is NameTriple n)
            {
                clone.Name = new NameTriple(n.First, n.Nick + " (Clone)", n.Last);
            }

            if (cfg.copySkills && source.skills != null && clone.skills != null)
            {
                foreach (SkillRecord sr in source.skills.skills)
                {
                    SkillRecord cr = clone.skills.GetSkill(sr.def);
                    if (cr == null) continue;
                    cr.levelInt = sr.levelInt;
                    cr.passion = sr.passion;
                    cr.xpSinceLastLevel = sr.xpSinceLastLevel;
                    cr.xpSinceMidnight = sr.xpSinceMidnight;
                }
            }

            if (cfg.copyTraits && source.story?.traits != null && clone.story?.traits != null)
            {
                clone.story.traits.allTraits.Clear();
                foreach (Trait t in source.story.traits.allTraits)
                    clone.story.traits.allTraits.Add(new Trait(t.def, t.Degree));
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

            if (cfg.copyGenes && source.genes != null && clone.genes != null)
            {
                foreach (Gene g in clone.genes.GenesListForReading.ToList())
                    clone.genes.RemoveGene(g);

                foreach (Gene g in source.genes.GenesListForReading)
                {
                    if (g.Overridden) continue;
                    clone.genes.AddGene(g.def, g.def.endogeneCategory != EndogeneCategory.None);
                }
            }

            if (cfg.copyAbilities && source.abilities != null && clone.abilities != null)
            {
                foreach (Ability ab in clone.abilities.abilities.ToList())
                    clone.abilities.RemoveAbility(ab.def);

                foreach (Ability ab in source.abilities.abilities)
                {
                    if (cfg.blockedAbilities.Contains(ab.def.defName)) continue;
                    clone.abilities.GainAbility(ab.def);
                }
            }

            if (cfg.copyApparel && source.apparel != null && clone.apparel != null)
            {
                foreach (Apparel ap in clone.apparel.WornApparel.ToList())
                { clone.apparel.Remove(ap); ap.Destroy(); }

                foreach (Apparel src in source.apparel.WornApparel)
                {
                    Apparel dst = (Apparel)ThingMaker.MakeThing(src.def, src.Stuff);

                    if (src.TryGetComp<CompColorable>() is CompColorable sc
                     && dst.TryGetComp<CompColorable>() is CompColorable dc)
                        dc.SetColor(sc.Color);

                    if (src.TryGetComp<CompQuality>() is CompQuality sq
                     && dst.TryGetComp<CompQuality>() is CompQuality dq)
                        dq.SetQuality(sq.Quality, ArtGenerationContext.Colony);

                    clone.apparel.Wear(dst, false);
                }
            }

            if (cfg.lifespanHediff != null)
                clone.health.AddHediff(HediffMaker.MakeHediff(cfg.lifespanHediff, clone));

            foreach (HediffDef bonus in cfg.bonusHediffs)
            {
                if (bonus == null || clone.health.hediffSet.HasHediff(bonus)) continue;
                clone.health.AddHediff(HediffMaker.MakeHediff(bonus, clone));
            }

            PortraitsCache.SetDirty(clone);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) => true;
    }
}