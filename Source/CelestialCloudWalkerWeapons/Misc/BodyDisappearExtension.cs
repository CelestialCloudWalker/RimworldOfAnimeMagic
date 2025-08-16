using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace AnimeArsenal
{
    public class BodyDisappearExtension : DefModExtension
    {
        public bool leaveAshFilth = true;
        public int filthAmount = 3;
        public string disappearMessage = "Body turned to ash!";
        public bool playEffect = true;
    }

    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Patch_Pawn_Kill
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (!HasBodyDisappearGene(__instance)) return;
            BodyDisappearUtility.RegisterPawnForDisappearance(__instance);
        }

        private static bool HasBodyDisappearGene(Pawn pawn)
        {
            if (pawn.genes == null) return false;

            return pawn.genes.GenesListForReading.Any(gene =>
                gene.Active && gene.def.GetModExtension<BodyDisappearExtension>() != null);
        }
    }

    public static class BodyDisappearUtility
    {
        private static HashSet<int> pawnsToDisappear = new HashSet<int>();

        public static void RegisterPawnForDisappearance(Pawn pawn)
        {
            if (pawn == null) return;
            pawnsToDisappear.Add(pawn.thingIDNumber);
        }

        public static void ProcessCorpseDisappearance(Corpse corpse)
        {
            if (corpse?.InnerPawn == null) return;
            if (!pawnsToDisappear.Contains(corpse.InnerPawn.thingIDNumber)) return;
            pawnsToDisappear.Remove(corpse.InnerPawn.thingIDNumber);

            var extension = GetBodyDisappearExtension(corpse.InnerPawn);
            if (extension == null) return;

            Map map = corpse.Map;
            IntVec3 position = corpse.Position;

            if (extension.playEffect && map != null)
            {
                EffecterDef effectDef = DefDatabase<EffecterDef>.GetNamed("Deflect_Metal");
                if (effectDef != null)
                {
                    Effecter disappearEffecter = effectDef.Spawn();
                    disappearEffecter.Trigger(new TargetInfo(position, map), new TargetInfo(position, map));
                    disappearEffecter.Cleanup();
                }

                if (!string.IsNullOrEmpty(extension.disappearMessage))
                {
                    MoteMaker.ThrowText(corpse.DrawPos, map, extension.disappearMessage, 3f);
                }
            }

            if (extension.leaveAshFilth && map != null)
            {
                FilthMaker.TryMakeFilth(position, map, ThingDefOf.Filth_Ash, extension.filthAmount);
            }

            corpse.Destroy();
        }

        private static BodyDisappearExtension GetBodyDisappearExtension(Pawn pawn)
        {
            if (pawn.genes == null) return null;

            var gene = pawn.genes.GenesListForReading.FirstOrDefault(g =>
                g.Active && g.def.GetModExtension<BodyDisappearExtension>() != null);

            return gene?.def.GetModExtension<BodyDisappearExtension>();
        }
    }

    public class MapComponent_BodyDisappear : MapComponent
    {
        public MapComponent_BodyDisappear(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (Find.TickManager.TicksGame % 10 != 0) return;

            List<Corpse> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .OfType<Corpse>()
                .ToList();

            foreach (Corpse corpse in corpses)
            {
                BodyDisappearUtility.ProcessCorpseDisappearance(corpse);
            }
        }
    }
}