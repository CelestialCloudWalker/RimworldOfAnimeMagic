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
            var map = parent.pawn.Map;
            var location = target.Cell;

            var delayedEffects = Current.Game.GetComponent<GameComponent_DelayedEffects>();
            delayedEffects?.StartLightningChain(map, location, parent.pawn, Props);
        }

        private int lastUsedTick = -999999;
        private const int aiCooldown = 300;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Find.TickManager.TicksGame < lastUsedTick + aiCooldown)
                return false;

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
        private List<LightningChainData> activeChains = new List<LightningChainData>();

        public GameComponent_DelayedEffects(Game game) { }

        public void StartLightningChain(Map map, IntVec3 target, Pawn caster, CompProperties_AbilityLightningStrike props)
        {
            var chainData = new LightningChainData
            {
                map = map,
                caster = caster,
                props = props,
                currentStrike = 0,
                nextStrikeTick = Find.TickManager.TicksGame,
                lastLocation = target,
                hitTargets = new HashSet<IntVec3>()
            };

            activeChains.Add(chainData);
        }

        public override void GameComponentTick()
        {
            for (int i = activeChains.Count - 1; i >= 0; i--)
            {
                var chain = activeChains[i];

                if (Find.TickManager.TicksGame >= chain.nextStrikeTick)
                {
                    ExecuteStrike(chain);
                    chain.currentStrike++;

                    if (chain.currentStrike >= chain.props.strikeCount)
                    {
                        activeChains.RemoveAt(i);
                    }
                    else
                    {
                        chain.nextStrikeTick = Find.TickManager.TicksGame + Mathf.RoundToInt(chain.props.strikeDelay * 60f);
                    }
                }
            }
        }

        private void ExecuteStrike(LightningChainData chain)
        {
            var location = chain.lastLocation;

            if (chain.currentStrike > 0 && chain.props.chainRange > 0)
            {
                location = FindChainTarget(chain);
            }

            var lightning = new WeatherEvent_LightningStrike(chain.map, location);
            lightning.FireEvent();

            var damage = chain.props.explosionDamage * Mathf.Pow(chain.props.damageEscalation, chain.currentStrike);

            if (chain.props.explosionRadius > 0f)
            {
                GenExplosion.DoExplosion(
                    location,
                    chain.map,
                    chain.props.explosionRadius,
                    DamageDefOf.Bomb,
                    chain.caster,
                    Mathf.RoundToInt(damage),
                    -1f
                );
            }

            chain.props.soundOnImpact?.PlayOneShot(new TargetInfo(location, chain.map));

            chain.hitTargets.Add(location);
            chain.lastLocation = location;
        }

        private IntVec3 FindChainTarget(LightningChainData chain)
        {
            var targets = new List<Pawn>();

            foreach (var pawn in chain.map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == chain.caster) continue;
                if (pawn.Position.DistanceTo(chain.lastLocation) > chain.props.chainRange) continue;
                if (chain.hitTargets.Contains(pawn.Position)) continue;
                if (pawn.Faction == chain.caster.Faction) continue;

                targets.Add(pawn);
            }

            if (targets.Count > 0)
            {
                var closest = targets.MinBy(p => p.Position.DistanceTo(chain.lastLocation));
                return closest.Position;
            }

            var randomCell = chain.lastLocation + IntVec3Utility.RandomHorizontalOffset(chain.props.chainRange);
            if (randomCell.InBounds(chain.map) && randomCell.Standable(chain.map))
                return randomCell;

            return chain.lastLocation;
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
        public IntVec3 lastLocation;
        public HashSet<IntVec3> hitTargets;
    }
}