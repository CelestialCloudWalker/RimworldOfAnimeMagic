using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class Dialog_DemonSpecialization : Window
    {
        private BloodDemonArtsGene sourceGene;
        private List<GeneDef> availableChoices;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_DemonSpecialization(BloodDemonArtsGene gene, List<GeneDef> choices)
        {
            sourceGene = gene;
            availableChoices = choices;
            doCloseButton = true;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f),
                $"Choose Demon Blood Arts for {sourceGene.pawn.Name.ToStringShort}");

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, inRect.width, 30f),
                $"Pawns Consumed: {sourceGene.TotalPawnsEaten} | Current Rank: {sourceGene.CurrentRank}");

            Rect scrollRect = new Rect(0f, 75f, inRect.width, inRect.height - 115f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, availableChoices.Count * 120f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float curY = 0f;
            foreach (GeneDef geneDef in availableChoices)
            {
                DrawSpecializationOption(new Rect(0f, curY, viewRect.width, 110f), geneDef);
                curY += 120f;
            }

            Widgets.EndScrollView();
        }

        private void DrawSpecializationOption(Rect rect, GeneDef geneDef)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.DrawBox(rect);

            Rect iconRect = new Rect(rect.x + 5f, rect.y + 5f, 60f, 60f);
            Widgets.DefIcon(iconRect, geneDef);

            Rect labelRect = new Rect(rect.x + 75f, rect.y + 5f, rect.width - 200f, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(labelRect, geneDef.label);

            Rect descRect = new Rect(rect.x + 75f, rect.y + 35f, rect.width - 200f, 65f);
            Text.Font = GameFont.Small;
            Widgets.Label(descRect, geneDef.description);

            Rect buttonRect = new Rect(rect.width - 110f, rect.y + 35f, 100f, 35f);
            if (Widgets.ButtonText(buttonRect, "Choose"))
            {
                sourceGene.ApplySpecialization(geneDef);
                Close();
            }

            Text.Font = GameFont.Small;
        }
    }
}
