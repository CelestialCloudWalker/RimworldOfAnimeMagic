using RimWorld;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompProperties_AbilityLightningStrike : CompProperties_AbilityEffect
    {
        public bool lightning = true;
        public float explosionRadius = 3f;
        public int explosionDamage = 50;
        public SoundDef soundOnImpact;

        
        public int strikeCount = 1;
        public float strikeDelay = 0.5f;
        public float chainRange = 0f;
        public float damageEscalation = 1f;

        public CompProperties_AbilityLightningStrike()
        {
            compClass = typeof(CompAbilityEffect_LightningStrike);
        }
    }

    public class CompAbilityEffect_LightningStrike : CompAbilityEffect
    {
        public new CompProperties_AbilityLightningStrike Props => (CompProperties_AbilityLightningStrike)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Map map = parent.pawn.Map;
            IntVec3 initialLocation = target.Cell;

            GameComponent_DelayedEffects delayedEffects = Current.Game.GetComponent<GameComponent_DelayedEffects>();
            if (delayedEffects != null)
            {
                delayedEffects.StartLightningChain(map, initialLocation, parent.pawn, Props);
            }
        }

        private int lastUsedTick = -999999;
        private const int aiDecisionCooldown = 300;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Find.TickManager.TicksGame < lastUsedTick + aiDecisionCooldown)
            {
                return false;
            }
            lastUsedTick = Find.TickManager.TicksGame;
            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return base.Valid(target, throwMessages) && target.Cell.Standable(parent.pawn.Map);
        }
    }

    public class GameComponent_DelayedEffects : GameComponent
    {
        private List<LightningChainData> activeLightningChains = new List<LightningChainData>();

        public GameComponent_DelayedEffects(Game game) { }

        public void StartLightningChain(Map map, IntVec3 initialTarget, Pawn caster, CompProperties_AbilityLightningStrike props)
        {
            LightningChainData chainData = new LightningChainData
            {
                map = map,
                caster = caster,
                props = props,
                currentStrike = 0,
                nextStrikeTick = Find.TickManager.TicksGame,
                lastStrikeLocation = initialTarget,
                hitTargets = new HashSet<IntVec3>()
            };

            activeLightningChains.Add(chainData);
        }

        public override void GameComponentTick()
        {
            for (int i = activeLightningChains.Count - 1; i >= 0; i--)
            {
                LightningChainData chain = activeLightningChains[i];

                if (Find.TickManager.TicksGame >= chain.nextStrikeTick)
                {
                    ExecuteLightningStrike(chain);
                    chain.currentStrike++;

                    if (chain.currentStrike >= chain.props.strikeCount)
                    {
                        activeLightningChains.RemoveAt(i);
                    }
                    else
                    {
                        chain.nextStrikeTick = Find.TickManager.TicksGame + Mathf.RoundToInt(chain.props.strikeDelay * 60f);
                    }
                }
            }
        }

        private void ExecuteLightningStrike(LightningChainData chain)
        {
            IntVec3 strikeLocation = chain.lastStrikeLocation;

            if (chain.currentStrike > 0 && chain.props.chainRange > 0)
            {
                strikeLocation = FindChainTarget(chain);
            }

            WeatherEvent_LightningStrike lightningStrike = new WeatherEvent_LightningStrike(chain.map, strikeLocation);
            lightningStrike.FireEvent();

            float currentDamage = chain.props.explosionDamage * Mathf.Pow(chain.props.damageEscalation, chain.currentStrike);

            if (chain.props.explosionRadius > 0f)
            {
                GenExplosion.DoExplosion(
                    strikeLocation,
                    chain.map,
                    chain.props.explosionRadius,
                    DamageDefOf.Bomb,
                    chain.caster,
                    Mathf.RoundToInt(currentDamage),
                    -1f
                );
            }

            if (chain.props.soundOnImpact != null)
            {
                chain.props.soundOnImpact.PlayOneShot(new TargetInfo(strikeLocation, chain.map));
            }

            chain.hitTargets.Add(strikeLocation);
            chain.lastStrikeLocation = strikeLocation;
        }

        private IntVec3 FindChainTarget(LightningChainData chain)
        {
            List<Pawn> potentialTargets = new List<Pawn>();

            foreach (Pawn pawn in chain.map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == chain.caster) continue;
                if (pawn.Position.DistanceTo(chain.lastStrikeLocation) > chain.props.chainRange) continue;
                if (chain.hitTargets.Contains(pawn.Position)) continue;
                if (pawn.Faction == chain.caster.Faction) continue; 

                potentialTargets.Add(pawn);
            }

            if (potentialTargets.Count > 0)
            {
                Pawn closestTarget = potentialTargets.MinBy(p => p.Position.DistanceTo(chain.lastStrikeLocation));
                return closestTarget.Position;
            }
            else
            {
                IntVec3 randomCell = chain.lastStrikeLocation + IntVec3Utility.RandomHorizontalOffset(chain.props.chainRange);
                if (randomCell.InBounds(chain.map) && randomCell.Standable(chain.map))
                {
                    return randomCell;
                }
                return chain.lastStrikeLocation; 
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }

    public class LightningChainData
    {
        public Map map;
        public Pawn caster;
        public CompProperties_AbilityLightningStrike props;
        public int currentStrike;
        public int nextStrikeTick;
        public IntVec3 lastStrikeLocation;
        public HashSet<IntVec3> hitTargets;
    }
}