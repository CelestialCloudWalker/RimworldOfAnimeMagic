using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_Effecter : CompProperties
    {
        public EffecterDef effecterDef;
        public bool attached = true;

        public CompProperties_Effecter()
        {
            compClass = typeof(Comp_Effecter);
        }
    }

    public class Comp_Effecter : ThingComp
    {
        private Effecter fx;

        public CompProperties_Effecter Props => (CompProperties_Effecter)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (Props.effecterDef != null)
            {
                fx = Props.effecterDef.Spawn();
                TriggerEffect();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            TriggerEffect();
        }

        private void TriggerEffect()
        {
            if (fx != null && parent.Spawned)
                fx.EffectTick(parent, parent);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map);
            fx?.Cleanup();
            fx = null;
        }
    }
}