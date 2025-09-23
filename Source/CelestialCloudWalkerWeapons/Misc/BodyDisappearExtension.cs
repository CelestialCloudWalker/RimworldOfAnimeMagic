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
            if (ShouldPawnDisappear(__instance))
            {
                BodyDisappearUtility.MarkForDisappearance(__instance);
            }
        }

        private static bool ShouldPawnDisappear(Pawn pawn)
        {
            if (pawn.genes == null) return false;

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active && gene.def.GetModExtension<BodyDisappearExtension>() != null)
                    return true;
            }
            return false;
        }
    }

    public static class BodyDisappearUtility
    {
        private static HashSet<int> markedPawns = new HashSet<int>();

        public static void MarkForDisappearance(Pawn pawn)
        {
            if (pawn != null)
                markedPawns.Add(pawn.thingIDNumber);
        }

        public static void HandleCorpseDisappearance(Corpse corpse)
        {
            if (corpse?.InnerPawn == null) return;

            int pawnId = corpse.InnerPawn.thingIDNumber;
            if (!markedPawns.Contains(pawnId)) return;

            markedPawns.Remove(pawnId);

            var settings = FindDisappearSettings(corpse.InnerPawn);
            if (settings == null) return;

            ApplyDisappearEffects(corpse, settings);
            corpse.Destroy();
        }

        private static BodyDisappearExtension FindDisappearSettings(Pawn pawn)
        {
            if (pawn.genes == null) return null;

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                if (gene.Active)
                {
                    var ext = gene.def.GetModExtension<BodyDisappearExtension>();
                    if (ext != null) return ext;
                }
            }
            return null;
        }

        private static void ApplyDisappearEffects(Corpse corpse, BodyDisappearExtension settings)
        {
            Map map = corpse.Map;
            IntVec3 pos = corpse.Position;

            if (settings.playEffect && map != null)
            {
                var effect = DefDatabase<EffecterDef>.GetNamed("Deflect_Metal");
                if (effect != null)
                {
                    var effecter = effect.Spawn();
                    effecter.Trigger(new TargetInfo(pos, map), new TargetInfo(pos, map));
                    effecter.Cleanup();
                }

                if (!settings.disappearMessage.NullOrEmpty())
                {
                    MoteMaker.ThrowText(corpse.DrawPos, map, settings.disappearMessage, 3f);
                }
            }

            if (settings.leaveAshFilth && map != null)
            {
                FilthMaker.TryMakeFilth(pos, map, ThingDefOf.Filth_Ash, settings.filthAmount);
            }
        }
    }

    public class MapComponent_BodyDisappear : MapComponent
    {
        public MapComponent_BodyDisappear(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            
            if (Find.TickManager.TicksGame % 10 != 0) return;

            var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>();

            foreach (var corpse in corpses.ToList())
            {
                BodyDisappearUtility.HandleCorpseDisappearance(corpse);
            }
        }
    }
}