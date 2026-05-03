using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_GaleHazard : CompProperties
    {
        public int durationTicks = 120;
        public float radius = 2f;
        public float damagePerTick = 4f;
        public DamageDef damageDef;
        public float armorPen = 0.2f;
        public int tickInterval = 15;
        public bool onlyAffectHostiles = true;
        public EffecterDef hazardEffecter;

        public CompProperties_GaleHazard()
        {
            compClass = typeof(CompGaleHazard);
        }
    }

    public class CompGaleHazard : ThingComp
    {
        public CompProperties_GaleHazard Props => (CompProperties_GaleHazard)props;

        public Pawn caster;

        private int ticksLeft;
        private int ticksSinceLastDamage;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ticksLeft = Props.durationTicks;
            ticksSinceLastDamage = 0;
        }

        public override void CompTick()
        {
            ticksLeft--;
            ticksSinceLastDamage++;

            if (ticksSinceLastDamage >= Props.tickInterval)
            {
                ticksSinceLastDamage = 0;
                DamageInRadius();
            }

            if (ticksLeft <= 0)
                parent.Destroy();
        }

        private void DamageInRadius()
        {
            Map map = parent.Map;
            if (map == null) return;

            DamageDef dmg = Props.damageDef ?? DamageDefOf.Cut;

            if (Props.hazardEffecter != null)
            {
                Effecter e = Props.hazardEffecter.Spawn(parent.Position, map);
                e.EffectTick(new TargetInfo(parent.Position, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                parent.Position, map, Props.radius, true))
            {
                if (!(thing is Pawn target)) continue;
                if (target.Dead || !target.Spawned) continue;
                if (target == caster) continue;

                if (Props.onlyAffectHostiles
                    && caster != null
                    && target.Faction != null
                    && !target.Faction.HostileTo(caster.Faction))
                    continue;

                target.TakeDamage(new DamageInfo(
                    dmg, Props.damagePerTick, Props.armorPen, -1f, caster));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
            Scribe_Values.Look(ref ticksSinceLastDamage, "ticksSinceLastDamage");
        }
    }
}