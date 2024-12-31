using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public class HediffCompProperties_DemonWorkSlaveEffect : HediffCompProperties
    {
        public int RegenerationTicks = 250;
        public int TicksBeforeBerserkWithoutMaster = 2000;
        public Color Color = new Color(0.2f, 0.7f, 0.2f, 1f);

        public HediffCompProperties_DemonWorkSlaveEffect()
        {
            compClass = typeof(HediffComp_DemonWorkSlaveEffect);
        }
    }


    public class HediffComp_DemonWorkSlaveEffect : HediffComp
    {
        public HediffCompProperties_DemonWorkSlaveEffect Props => (HediffCompProperties_DemonWorkSlaveEffect)props;

        Need_Suppression _Suppression;
        Need_Suppression Suppression
        {
            get
            {
                if (_Suppression == null)
                {
                    _Suppression = parent.pawn.needs.TryGetNeed<Need_Suppression>();
                }

                return _Suppression;
            }
        }


        protected Faction OriginalFaction;

        protected Pawn Master;
        protected int CurrentTick = 0;
        protected int TicksWithoutMaster = 0;
        private Color originalColor;
        private bool colorChanged = false;
        public void SetSlaveMaster(Pawn Pawn)
        {
            Master = Pawn;
            ApplyZombieColor(Pawn);
            CurrentTick = 0;
            TicksWithoutMaster = 0;
        }


        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RestoreOriginalColor(Pawn);
        }

       

        private void HandleNeedsAndSupress(Pawn pawn)
        {
            // Automatically satisfy needs
            foreach (Need need in pawn.needs.AllNeeds)
            {
                need.CurLevel = need.MaxLevel;
            }

            if (Suppression != null && Suppression.CurLevelPercentage <= 0.94f)
            {
                SlaveRebellionUtility.IncrementSuppression(Suppression, pawn, pawn, 5);
            }
        }

        private void ApplyZombieColor(Pawn pawn)
        {
            //originalColor = pawn.Drawer.renderer.BodyGraphic.color;
            //pawn.Drawer.renderer.BodyGraphic.color = Props.Color;
            //pawn.Drawer.renderer.SetAllGraphicsDirty();
            //colorChanged = true;
        }

        private void RestoreOriginalColor(Pawn pawn)
        {
            //pawn.Drawer.renderer.BodyGraphic.color = originalColor;
            //pawn.Drawer.renderer.SetAllGraphicsDirty();
            //colorChanged = false;
        }


        private void TriggerBerserk(Pawn pawn)
        {
            pawn.guest.SetGuestStatus(OriginalFaction, GuestStatus.Guest);
            pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, null, true, true);
            // pawn.health.RemoveHediff(parent);
        }
        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_References.Look(ref Master, "pawnSlaveMaster");
            Scribe_Values.Look(ref CurrentTick, "slaveCurrentRegenTick");
            Scribe_Values.Look(ref TicksWithoutMaster, "slaveTickWithoutMaster");
            Scribe_References.Look(ref OriginalFaction, "slaveOriginalFaction");
        }
    }

}