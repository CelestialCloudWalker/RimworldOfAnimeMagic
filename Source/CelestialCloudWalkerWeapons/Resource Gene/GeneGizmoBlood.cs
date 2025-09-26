using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    [StaticConstructorOnStartup]
    public class GeneGizmoBlood : GeneGizmo_ResourceAstral
    {
        private BloodDemonArtsGene BloodDemonArtsGene => (BloodDemonArtsGene)gene;
        protected override bool IsDraggable => false;
        private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));

        public GeneGizmoBlood(BloodDemonArtsGene gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float progress = BloodDemonArtsGene.ExhaustionProgress;
            DrawDraggableBarTarget(barRect, progress, DragBarTex);
            return result;
        }

        protected override string GetTooltip()
        {
            try
            {
                if (BloodDemonArtsGene?.pawn == null)
                {
                    return "Blood Art Resource";
                }

                string text = "";
                try
                {
                    // Calculate total blood directly (base value + bonus from pawns eaten)
                    float baseBlood = BloodDemonArtsGene.Value;
                    float totalBlood = baseBlood; // For now, just use base value until we can access bonusBloodFromPawns
                    float maxBlood = BloodDemonArtsGene.pawn.GetStatValue(BloodDemonArtsGene.Def.maxStat);

                    text = string.Format("{0}: {1} / {2}",
                        BloodDemonArtsGene.Def?.resourceLabel?.CapitalizeFirst()?.Colorize(ColoredText.TipSectionTitleColor) ?? "Blood Art",
                        totalBlood.ToString("F0"),
                        maxBlood.ToString("F0"));
                }
                catch
                {
                    return "Blood Art Resource";
                }

                // Add demon progression info safely
                try
                {
                    text += "\n\n" + "Demon Progression".Colorize(ColoredText.TipSectionTitleColor);
                    text += string.Format("\nCurrent Rank: {0}", BloodDemonArtsGene.CurrentRank.ToString());
                    text += string.Format("\nPawns Eaten: {0}", BloodDemonArtsGene.TotalPawnsEaten);

                    // Show progress to next rank
                    var progressionExt = BloodDemonArtsGene.def?.GetModExtension<DemonProgressionExtension>();
                    if (progressionExt?.pawnsRequiredPerRank != null && progressionExt.pawnsRequiredPerRank.Count > 0)
                    {
                        int rankIndex = (int)BloodDemonArtsGene.CurrentRank;
                        if (rankIndex >= 0 && rankIndex < progressionExt.pawnsRequiredPerRank.Count)
                        {
                            int pawnsNeeded = progressionExt.pawnsRequiredPerRank[rankIndex];
                            int progress = BloodDemonArtsGene.TotalPawnsEaten;
                            text += string.Format("\nNext Rank: {0}/{1} pawns", progress, pawnsNeeded);
                        }
                        else
                        {
                            text += "\nRank: MAX";
                        }
                    }
                }
                catch
                {
                    // Skip demon info if there's an error
                }

                // Add regeneration info safely
                try
                {
                    if (BloodDemonArtsGene.RegenAmount > 0)
                    {
                        text += string.Format("\n\nRegenerates {0} every {1} ticks",
                            BloodDemonArtsGene.RegenAmount, BloodDemonArtsGene.RegenTicks);
                    }
                }
                catch { }

                // Add description safely
                try
                {
                    if (BloodDemonArtsGene.Def?.resourceDescription != null && !BloodDemonArtsGene.Def.resourceDescription.NullOrEmpty())
                    {
                        text += "\n\n" + BloodDemonArtsGene.Def.resourceDescription;
                    }
                }
                catch { }

                return text;
            }
            catch
            {
                return "Blood Art Resource";
            }
        }

        private static void DrawDraggableBarTarget(Rect rect, float percent, Texture2D targetTex)
        {
            float num = Mathf.Round((rect.width - 8f) * percent);
            GUI.DrawTexture(new Rect
            {
                x = rect.x + 3f + num,
                y = rect.y,
                width = 2f,
                height = rect.height
            }, targetTex);
            float num2 = UIScaling.AdjustCoordToUIScalingFloor(rect.x + 2f + num);
            float xMax = UIScaling.AdjustCoordToUIScalingCeil(num2 + 4f);
            Rect rect2 = new Rect
            {
                y = rect.y - 3f,
                height = 5f,
                xMin = num2,
                xMax = xMax
            };
            GUI.DrawTexture(rect2, targetTex);
            Rect position = rect2;
            position.y = rect.yMax - 2f;
            GUI.DrawTexture(position, targetTex);
        }
    }
}