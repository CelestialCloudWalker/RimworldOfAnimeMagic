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
        private static readonly Texture2D SanityFullTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.2f));
        private static readonly Texture2D SanityLowTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.9f, 0.5f, 0.1f));
        private static readonly Texture2D SanityCriticalTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.9f, 0.1f, 0.1f));
        private static readonly Texture2D SanityEmptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));

        public GeneGizmoBlood(BloodDemonArtsGene gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor)
            : base(gene, drainGenes, barColor, barHighlightColor)
        {
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUI(topLeft, maxWidth, parms);

            float exhaustionProgress = BloodDemonArtsGene.ExhaustionProgress;
            DrawDraggableBarTarget(barRect, exhaustionProgress, DragBarTex);


            return result;
        }

        private void DrawSanityBar(Vector2 topLeft, float maxWidth)
        {
            float yOffset = 75f; 
            float gizmoWidth = Mathf.Min(140f, maxWidth);
            Rect sanityRect = new Rect(topLeft.x, topLeft.y + yOffset, gizmoWidth, 30f);

            Widgets.DrawWindowBackground(sanityRect);

            Rect innerRect = sanityRect.ContractedBy(4f);

         
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect labelRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 12f);
            Widgets.Label(labelRect, "Sanity");

           
            Rect barBgRect = new Rect(innerRect.x, innerRect.y + 12f, innerRect.width, 12f);
            Widgets.DrawBoxSolid(barBgRect, SanityEmptyTex.GetPixel(0, 0));

            float sanityPercent = BloodDemonArtsGene.CurrentSanity / BloodDemonArtsGene.MaxSanity;
            Rect fillRect = new Rect(barBgRect.x, barBgRect.y, barBgRect.width * sanityPercent, barBgRect.height);

            Color barColor = GetSanityColor(sanityPercent);
            Widgets.DrawBoxSolid(fillRect, barColor);
            Widgets.DrawBox(barBgRect);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            string valueText = $"{BloodDemonArtsGene.CurrentSanity:F0}/{BloodDemonArtsGene.MaxSanity:F0}";
            GUI.color = Color.white;
            Widgets.Label(barBgRect, valueText);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private Color GetSanityColor(float percent)
        {
            var ext = BloodDemonArtsGene.def?.GetModExtension<DemonSanityExtension>();
            if (ext == null) return SanityFullTex.GetPixel(0, 0);

            float critical = ext.criticalSanityThreshold / ext.maxSanity;
            float low = ext.lowSanityThreshold / ext.maxSanity;

            if (percent <= critical)
                return SanityCriticalTex.GetPixel(0, 0);
            else if (percent <= low)
                return SanityLowTex.GetPixel(0, 0);
            else
                return SanityFullTex.GetPixel(0, 0);
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
                    float baseBlood = BloodDemonArtsGene.Value;
                    float totalBlood = baseBlood;
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

                var sanityExt = BloodDemonArtsGene.def?.GetModExtension<DemonSanityExtension>();
                if (sanityExt != null)
                {
                    try
                    {
                        text += "\n\n" + "Demon Sanity".Colorize(ColoredText.TipSectionTitleColor);

                        float sanityPercent = BloodDemonArtsGene.CurrentSanity / BloodDemonArtsGene.MaxSanity;
                        Color sanityColor = sanityPercent <= 0.25f ? Color.red :
                                          sanityPercent <= 0.5f ? new Color(1f, 0.5f, 0f) :
                                          Color.green;

                        text += string.Format("\nCurrent: {0} / {1} ({2})",
                            BloodDemonArtsGene.CurrentSanity.ToString("F0"),
                            BloodDemonArtsGene.MaxSanity.ToString("F0"),
                            sanityPercent.ToStringPercent().Colorize(sanityColor));

                        try
                        {
                            var ticksSinceLastMealField = typeof(BloodDemonArtsGene).GetField("ticksSinceLastMeal",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                            if (ticksSinceLastMealField != null)
                            {
                                object fieldValue = ticksSinceLastMealField.GetValue(BloodDemonArtsGene);
                                int ticksSinceLastMeal = fieldValue != null ? (int)fieldValue : 0;
                                int daysSinceLastMeal = ticksSinceLastMeal / 60000;

                                Color daysColor = daysSinceLastMeal == 0 ? Color.green :
                                                daysSinceLastMeal < 2 ? Color.yellow :
                                                Color.red;

                                text += string.Format("\nDays Since Fed: {0}",
                                    daysSinceLastMeal.ToString().Colorize(daysColor));
                            }
                        }
                        catch { }

                        text += "\n\n" + "Sanity Effects".Colorize(ColoredText.TipSectionTitleColor);
                        text += string.Format("\nRegeneration: {0}",
                            BloodDemonArtsGene.RegenMultiplier.ToStringPercent().Colorize(
                                BloodDemonArtsGene.RegenMultiplier >= 1f ? Color.green : Color.yellow));
                        text += string.Format("\nDamage Output: {0}",
                            BloodDemonArtsGene.DamageMultiplier.ToStringPercent().Colorize(
                                BloodDemonArtsGene.DamageMultiplier >= 1f ? Color.green : Color.yellow));

                        string status = GetSanityStatusText(sanityPercent, sanityExt);
                        text += string.Format("\nStatus: {0}", status);

                        if (sanityPercent <= (sanityExt.mentalBreakThreshold / sanityExt.maxSanity))
                        {
                            text += "\n" + "WILL HUNT HUMANS!".Colorize(Color.red);
                        }
                    }
                    catch { }
                }

                try
                {
                    text += "\n\n" + "Demon Progression".Colorize(ColoredText.TipSectionTitleColor);
                    text += string.Format("\nCurrent Rank: {0}", BloodDemonArtsGene.CurrentRank.ToString());
                    text += string.Format("\nPawns Eaten: {0}", BloodDemonArtsGene.TotalPawnsEaten);

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

                    if (sanityExt != null)
                    {
                        text += string.Format("\nSanity/Pawn: +{0}", sanityExt.sanityRestoredPerPawnEaten);
                    }
                }
                catch { }

                try
                {
                    var map = BloodDemonArtsGene.pawn.Map;
                    if (map != null)
                    {
                        var sunlightComp = map.GetComponent<MapComponent_SunlightDamage>();
                        if (sunlightComp != null)
                        {
                            text += "\n\n" + "Sunlight Status".Colorize(ColoredText.TipSectionTitleColor);

                            float tolerancePercent = sunlightComp.GetSunTolerancePercentage(BloodDemonArtsGene.pawn);
                            float damagePercent = sunlightComp.GetSunlightDamagePercentage(BloodDemonArtsGene.pawn);

                            Color toleranceColor = tolerancePercent > 50f ? Color.green :
                                                   tolerancePercent > 20f ? Color.yellow :
                                                   Color.red;

                            text += string.Format("\nSun Tolerance: {0}",
                                tolerancePercent.ToString("F1").Colorize(toleranceColor) + "%");

                            if (damagePercent > 0)
                            {
                                Color damageColor = damagePercent < 50f ? Color.yellow : Color.red;
                                text += string.Format("\nSun Damage: {0}",
                                    damagePercent.ToString("F1").Colorize(damageColor) + "%");
                            }

                            float coverage = sunlightComp.GetArmorCoverage(BloodDemonArtsGene.pawn);
                            bool hasHeadCover = sunlightComp.GetHeadCoverage(BloodDemonArtsGene.pawn);

                            text += string.Format("\nBody Coverage: {0:P0}", coverage);
                            text += string.Format("\nHead: {0}", hasHeadCover ? "Protected".Colorize(Color.green) : "EXPOSED".Colorize(Color.red));
                        }
                    }
                }
                catch { }

                try
                {
                    if (BloodDemonArtsGene.RegenAmount > 0)
                    {
                        text += string.Format("\n\nRegenerates {0} every {1} ticks",
                            BloodDemonArtsGene.RegenAmount, BloodDemonArtsGene.RegenTicks);
                    }
                }
                catch { }

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

        private string GetSanityStatusText(float percent, DemonSanityExtension ext)
        {
            float critical = ext.criticalSanityThreshold / ext.maxSanity;
            float low = ext.lowSanityThreshold / ext.maxSanity;
            float mentalBreak = ext.mentalBreakThreshold / ext.maxSanity;

            if (percent <= mentalBreak)
                return "RAVENOUS".Colorize(Color.red);
            else if (percent <= critical * 0.5f)
                return "Starving".Colorize(Color.red);
            else if (percent <= critical)
                return "Critical Hunger".Colorize(new Color(1f, 0.5f, 0f));
            else if (percent <= low)
                return "Hungry".Colorize(Color.yellow);
            else if (percent >= 0.9f)
                return "Satiated".Colorize(Color.green);
            else
                return "Normal".Colorize(Color.white);
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