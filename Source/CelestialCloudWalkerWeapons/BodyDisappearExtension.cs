using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace AnimeArsenal
{
    // Extension that can be added to your gene def
    public class BodyDisappearExtension : DefModExtension
    {
        public bool leaveAshFilth = true;
        public int filthAmount = 3;
        public string disappearMessage = "Body turned to ash!";
        public bool playEffect = true;
    }

    // Harmony patch to handle death
    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Patch_Pawn_Kill
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            // Check if the pawn has the BloodDemonArt gene
            if (!HasBloodDemonGene(__instance)) return;

            // Handle post-death logic in the next tick to ensure the corpse exists
            BodyDisappearUtility.RegisterPawnForDisappearance(__instance);
        }

        // Helper method to check if pawn has the BloodDemonArt gene
        private static bool HasBloodDemonGene(Pawn pawn)
        {
            return pawn.genes?.HasGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) ?? false;
        }
    }

    // Utility class to handle the body disappearance
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

            // Remove from tracking
            pawnsToDisappear.Remove(corpse.InnerPawn.thingIDNumber);

            // Get the extension settings
            var extension = DefDatabase<GeneDef>.GetNamed("BloodDemonArt")
                .GetModExtension<BodyDisappearExtension>();

            if (extension == null) return;

            Map map = corpse.Map;
            IntVec3 position = corpse.Position;

            // Show disappearing effect if enabled
            if (extension.playEffect && map != null)
            {
                // Use a built-in effect instead of custom one to avoid compatibility issues
                EffecterDef effectDef = DefDatabase<EffecterDef>.GetNamed("Deflect_Metal");
                if (effectDef != null)
                {
                    Effecter disappearEffecter = effectDef.Spawn();
                    disappearEffecter.Trigger(new TargetInfo(position, map), new TargetInfo(position, map));
                    disappearEffecter.Cleanup();
                }

                // Create message
                if (!string.IsNullOrEmpty(extension.disappearMessage))
                {
                    MoteMaker.ThrowText(corpse.DrawPos, map, extension.disappearMessage, 3f);
                }
            }

            // Create ash filth if enabled
            if (extension.leaveAshFilth && map != null)
            {
                FilthMaker.TryMakeFilth(position, map, ThingDefOf.Filth_Ash, extension.filthAmount);
            }

            // Destroy the corpse
            corpse.Destroy();
        }
    }

    // Map component to process disappearances
    public class MapComponent_BodyDisappear : MapComponent
    {
        public MapComponent_BodyDisappear(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            // Only process once every few ticks to reduce performance impact
            if (Find.TickManager.TicksGame % 10 != 0) return;

            // Find all corpses of pawns who should disappear
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