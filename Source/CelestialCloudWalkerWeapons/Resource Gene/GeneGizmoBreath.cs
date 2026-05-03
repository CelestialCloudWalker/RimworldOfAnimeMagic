using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    [StaticConstructorOnStartup]
    public class GeneGizmoBreath : GeneGizmo_ResourceAstral
    {
        private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));
        private static readonly Color DefaultBreathColor = new Color(0.35f, 0.40f, 0.45f);

        private BreathingTechniqueGene BreathGene => gene as BreathingTechniqueGene;
        private DeathTimerManager.DeathTimerData DeathTimer => gene.GetDeathTimer();
        protected override bool IsDraggable => false;

        public GeneGizmoBreath(BreathingTechniqueGene gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor)
            : base(gene, drainGenes, barColor, barHighlightColor) { }

        private BreathingTechniqueGene GetActiveStyleGene()
        {
            if (BreathGene?.pawn?.genes == null) return null;
            foreach (var g in BreathGene.pawn.genes.GenesListForReading)
                if (g.Active && g is BreathingTechniqueGene btg && btg.IsStyleGene)
                    return btg;
            return null;
        }

        private static readonly Dictionary<string, Color> StyleColors = new Dictionary<string, Color>
        {
            { "Gene_WaterBreath",   new Color(0.0f,  0.412f, 0.580f) },
            { "Gene_WindBreath",    new Color(0.196f, 0.804f, 0.196f) },
            { "Gene_FlameBreath",   new Color(1.0f,  0.271f, 0.0f)   },
            { "Gene_StoneBreath",   new Color(0.663f, 0.663f, 0.663f) },
            { "Gene_ThunderBreath", new Color(0.0f,  0.749f, 1.0f)   },
            { "Gene_MistBreath",    new Color(0.753f, 0.753f, 0.753f) },
            { "Gene_FlowerBreath",  new Color(1.0f,  0.714f, 0.757f) },
            { "Gene_SerpentBreath", new Color(0.541f, 0.169f, 0.886f) },
            { "Gene_SoundBreath",   new Color(1.0f,  0.843f, 0.0f)   },
            { "Gene_InsectBreath",  new Color(0.855f, 0.439f, 0.839f) },
            { "Gene_LoveBreath",    new Color(1.0f,  0.714f, 0.757f) },
            { "Gene_FrostBreath",   new Color(0.678f, 0.847f, 0.902f) },
            { "Gene_ForestBreath",  new Color(0.333f, 0.420f, 0.184f) },
            { "Gene_BeastBreath",   new Color(0.545f, 0.271f, 0.075f) },
            { "Gene_MoonBreath",    new Color(0.541f, 0.169f, 0.886f) },
            { "Gene_SunBreath",     new Color(1.0f,  0.271f, 0.0f)   },
        };

        private Color GetBarColor()
        {
            var style = GetActiveStyleGene();
            if (style?.def?.defName != null && StyleColors.TryGetValue(style.def.defName, out Color c))
                return c;
            return DefaultBreathColor;
        }

        protected override Color BarColor => GetBarColor();
        protected override Color BarHighlightColor => GetBarColor() * 1.2f;

        protected override string Title
        {
            get
            {
                var style = GetActiveStyleGene();
                if (style != null)
                    return !string.IsNullOrEmpty(style.Def?.resourceLabel)
                        ? style.Def.resourceLabel
                        : style.Def?.label ?? "Breath";
                return !string.IsNullOrEmpty(BreathGene?.Def?.resourceLabel)
                    ? BreathGene.Def.resourceLabel
                    : "Breathing Potential";
            }
        }

        private static readonly Color BarBgColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float progress = BreathGene?.ExhaustionProgress ?? 0f;

            Color targetColor = GetBarColor();

            Widgets.DrawBoxSolid(barRect, BarBgColor);

            float fillWidth = barRect.width * (BreathGene?.ValuePercent ?? 0f);
            if (fillWidth > 0f)
                Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), targetColor);

            Widgets.DrawBox(barRect);

            if (BreathGene != null)
            {
                string label = $"{BreathGene.ValueForDisplay} / {BreathGene.MaxForDisplay}";
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            DrawDraggableBarTarget(barRect, progress, DragBarTex);
            return result;
        }


        protected override string GetTooltip()
        {
            if (BreathGene == null)
                return base.GetTooltip();

            var styleGene = GetActiveStyleGene();
            var styleProgExt = styleGene?.def?.GetModExtension<BreathingProgressionExtension>();

            string resourceName = styleGene != null
                ? (!string.IsNullOrEmpty(styleGene.Def?.resourceLabel)
                    ? styleGene.Def.resourceLabel
                    : styleGene.Def?.label ?? "Breath")
                : (!string.IsNullOrEmpty(BreathGene.Def?.resourceLabel)
                    ? BreathGene.Def.resourceLabel
                    : "Breathing Potential");

            string tip = $"{resourceName.Colorize(ColoredText.TipSectionTitleColor)}: " +
                         $"{BreathGene.ValueForDisplay} / {BreathGene.MaxForDisplay}\n";

            float exhaustPct = BreathGene.ExhaustionProgress;
            string exhaustStatus;
            Color exhaustColor;

            if (BreathGene.isExhausted)
            {
                exhaustStatus = "Exhausted — recovering";
                exhaustColor = Color.red;
            }
            else if (exhaustPct >= 0.75f)
            {
                exhaustStatus = "Nearing exhaustion";
                exhaustColor = new Color(1f, 0.5f, 0f);
            }
            else if (exhaustPct >= 0.4f)
            {
                exhaustStatus = "Moderate strain";
                exhaustColor = Color.yellow;
            }
            else
            {
                exhaustStatus = "Fresh";
                exhaustColor = Color.green;
            }

            tip += $"\n{"Breathing Status".Colorize(ColoredText.TipSectionTitleColor)}";
            tip += $"\nExhaustion: {(exhaustPct * 100f):F0}%  —  {exhaustStatus.Colorize(exhaustColor)}";

            int totalKills = BreathGene.TotalKills;
            int totalDemonKills = BreathGene.TotalDemonKills;

            tip += $"\n\n{"Kill Tracker".Colorize(ColoredText.TipSectionTitleColor)}";
            tip += $"\nTotal Kills: {totalKills}";
            tip += $"\nDemon Kills: {totalDemonKills}";

            if (!BreathGene.HasSpecialized)
            {
                var potProgExt = BreathGene.def?.GetModExtension<BreathingProgressionExtension>();
                if (potProgExt != null)
                {
                    tip += $"\n\n{"Style Mastery".Colorize(ColoredText.TipSectionTitleColor)}";

                    int needed = potProgExt.killsRequiredForSpecialization;
                    int demonNeeded = potProgExt.demonKillsRequiredForSpecialization;

                    if (potProgExt.specializationUnlockMode == BreathingKillTrackerMode.DemonOnly)
                    {
                        float pct = Mathf.Clamp01((float)totalDemonKills / demonNeeded);
                        tip += $"\nDemon kills: {totalDemonKills} / {demonNeeded}";
                        tip += $"  ({(pct * 100f):F0}%)".Colorize(Color.Lerp(Color.red, Color.green, pct));
                    }
                    else if (potProgExt.specializationUnlockMode == BreathingKillTrackerMode.Both)
                    {
                        float pct1 = Mathf.Clamp01((float)totalKills / needed);
                        float pct2 = Mathf.Clamp01((float)totalDemonKills / demonNeeded);
                        tip += $"\nKills: {totalKills}/{needed}  ({(pct1 * 100f):F0}%)".Colorize(Color.Lerp(Color.red, Color.green, pct1));
                        tip += $"\nDemon kills: {totalDemonKills}/{demonNeeded}  ({(pct2 * 100f):F0}%)".Colorize(Color.Lerp(Color.red, Color.green, pct2));
                    }
                    else
                    {
                        float pct = Mathf.Clamp01((float)totalKills / needed);
                        tip += $"\nKills: {totalKills} / {needed}  ({(pct * 100f):F0}%)".Colorize(Color.Lerp(Color.red, Color.green, pct));
                    }

                    if (BreathGene.CanSpecialize())
                    {
                        int available = BreathGene.GetUnlockedSpecializations().Count;
                        tip += $"\n{"▶ Ready to choose a breathing style!".Colorize(Color.green)}";
                        tip += $"\n  {available} style(s) available".Colorize(Color.cyan);
                    }
                }
            }
            else
            {
                tip += $"\n\n{"Style Mastery".Colorize(ColoredText.TipSectionTitleColor)}";

                if (styleGene != null)
                    tip += $"\n{(styleGene.Def?.label ?? "Unknown style").Colorize(Color.cyan)} mastered.";
                else
                    tip += $"\n{"Style mastered.".Colorize(Color.cyan)}";

                if (styleProgExt?.abilityUnlocks != null && styleProgExt.abilityUnlocks.Count > 0)
                {
                    var unlockedIndices = BreathGene.GetUnlockedAbilityIndices();
                    int totalUnlocked = unlockedIndices.Count;
                    int totalUnlocks = styleProgExt.abilityUnlocks.Count;

                    tip += $"\n\n{"Ability Unlocks".Colorize(ColoredText.TipSectionTitleColor)}";
                    tip += $"\nUnlocked: {totalUnlocked} / {totalUnlocks}";

                    for (int i = 0; i < styleProgExt.abilityUnlocks.Count; i++)
                    {
                        if (unlockedIndices.Contains(i)) continue;
                        var unlock = styleProgExt.abilityUnlocks[i];
                        string unlockName = unlock.ability?.label ?? unlock.hediff?.label ?? "Unknown";
                        tip += $"\nNext: {unlockName.Colorize(Color.yellow)} at {unlock.killsRequired} kills";
                        break;
                    }
                }
            }

            tip += $"\n\nRegenerates {BreathGene.GetCurrentRegenAmountPublic():F0} every {BreathGene.GetCurrentRegenTicksPublic()} ticks";

            string desc = BreathGene.Def?.resourceDescription;
            if (!string.IsNullOrEmpty(desc))
                tip += $"\n\n{desc}";

            return tip;
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