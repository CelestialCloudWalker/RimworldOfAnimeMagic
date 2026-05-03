using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
namespace AnimeArsenal
{
    public class CompProperties_FireworksShow : CompProperties_AbilityEffect
    {
        public int bombCount = 6;
        public float scatterRadius = 5f;
        public ThingDef projectileDef = null;
        public List<GeneDef> targetGenes = new List<GeneDef>();
        public DamageDef damageDef = null;
        public float damageAmount = 25f;
        public float explosionRadius = 3f;
        public float armorPenetration = 0.3f;
        public HediffDef applyHediff = null;
        public float hediffSeverity = 0.5f;
        public EffecterDef casterEffecter = null;
        public EffecterDef impactEffecter = null;
        public EffecterDef explosionEffecter = null;
        public SoundDef bombSound = null;
        public bool onlyHostileBuildings = true;
        public CompProperties_FireworksShow()
        {
            compClass = typeof(CompAbilityEffect_FireworksShow);
        }
    }
    public class CompAbilityEffect_FireworksShow : CompAbilityEffect
    {
        public new CompProperties_FireworksShow Props => (CompProperties_FireworksShow)props;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;
            if (Props.casterEffecter != null)
            {
                Effecter e = Props.casterEffecter.Spawn();
                e.Trigger(new TargetInfo(caster.Position, map), new TargetInfo(caster.Position, map));
                e.Cleanup();
            }
            IntVec3 center = target.Cell;
            for (int i = 0; i < Props.bombCount; i++)
            {
                IntVec3 cell = center + new IntVec3(
                    Mathf.RoundToInt(Rand.Range(-Props.scatterRadius, Props.scatterRadius)),
                    0,
                    Mathf.RoundToInt(Rand.Range(-Props.scatterRadius, Props.scatterRadius))
                );
                if (!cell.InBounds(map)) cell = center;
                if (Props.projectileDef != null)
                {
                    Projectile proj = (Projectile)GenSpawn.Spawn(
                        Props.projectileDef, caster.Position, map, WipeMode.Vanish);
                    proj.Launch(caster, caster.DrawPos,
                        new LocalTargetInfo(cell), new LocalTargetInfo(cell),
                        ProjectileHitFlags.IntendedTarget);
                }
                else
                {
                    AntiDemonBombUtility.DetonateAt(
                        cell, map, caster,
                        Props.targetGenes, Props.damageDef,
                        Props.damageAmount, Props.explosionRadius,
                        Props.armorPenetration, Props.applyHediff,
                        Props.hediffSeverity, Props.impactEffecter,
                        Props.explosionEffecter, Props.bombSound,
                        Props.onlyHostileBuildings);
                }
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent?.pawn != null;
        }
    }
}