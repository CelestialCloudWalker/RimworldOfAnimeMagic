using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class Dialog_BreathingSpecialization : Window
    {
        private BreathingTechniqueGene breathingGene;
        private List<GeneDef> availableSpecializations;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_BreathingSpecialization(BreathingTechniqueGene gene, List<GeneDef> specializations)
        {
            breathingGene = gene;
            availableSpecializations = specializations;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40f), "Choose Breathing Style");
            Text.Font = GameFont.Small;

            float y = inRect.y + 50f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 25f),
                $"Total Kills: {breathingGene.TotalKills}   Demon Kills: {breathingGene.TotalDemonKills}");
            y += 30f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 25f),
                "Once chosen, this cannot be undone. Choose carefully.");
            y += 35f;

            Rect scrollRect = new Rect(inRect.x, y, inRect.width, inRect.height - y - 50f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, availableSpecializations.Count * 110f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float entryY = 0f;
            foreach (var spec in availableSpecializations)
            {
                Rect entryRect = new Rect(0f, entryY, viewRect.width, 100f);
                Widgets.DrawMenuSection(entryRect);

                Rect iconRect = new Rect(entryRect.x + 5f, entryRect.y + 5f, 64f, 64f);
                if (spec.Icon != null && spec.Icon != BaseContent.BadTex)
                    Widgets.DrawTextureFitted(iconRect, spec.Icon, 1f);

                Rect textRect = new Rect(iconRect.xMax + 10f, entryRect.y + 5f,
                    entryRect.width - iconRect.width - 110f, entryRect.height - 10f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, 25f), spec.LabelCap);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(textRect.x, textRect.y + 28f, textRect.width, textRect.height - 28f),
                    spec.description ?? "");
                Text.Font = GameFont.Small;

                Rect btnRect = new Rect(entryRect.xMax - 100f, entryRect.y + 30f, 90f, 35f);
                if (Widgets.ButtonText(btnRect, "Choose"))
                {
                    breathingGene.ApplySpecialization(spec);
                    Close();
                }

                entryY += 110f;
            }

            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 60f, inRect.yMax - 40f, 120f, 35f), "Cancel"))
                Close();
        }
    }
}