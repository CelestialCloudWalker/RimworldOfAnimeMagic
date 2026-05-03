using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class HediffCompProperties_DemonWorkSlaveEffect : HediffCompProperties
    {
        public int regenerationTicks = 250;
        public bool checkMasterDistance = true;
        public float masterMaxDistance = 30f;
        public int ticksBeforeBerserkWithoutMaster = 2000;
        public bool goFeralOnMasterDeath = true;
        public bool goFeralOnHediffExpired = true;
        public Color slaveColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        public bool applyColorTint = false;
        public string masterDeathMessage = "{0} has gone feral — their master is dead.";
        public string masterTooFarMessage = "{0} is losing their bond with their master.";
        public string berserkMessage = "{0} has broken free and gone berserk!";

        public HediffCompProperties_DemonWorkSlaveEffect()
        {
            compClass = typeof(HediffComp_DemonWorkSlaveEffect);
        }
    }

    public class HediffComp_DemonWorkSlaveEffect : HediffComp
    {
        public new HediffCompProperties_DemonWorkSlaveEffect Props =>
            (HediffCompProperties_DemonWorkSlaveEffect)props;

        private Need_Suppression _suppression;
        private Need_Suppression Suppression
        {
            get
            {
                if (_suppression == null)
                    _suppression = Pawn?.needs?.TryGetNeed<Need_Suppression>();
                return _suppression;
            }
        }

        protected Faction OriginalFaction;
        protected Pawn Master;
        protected int CurrentTick = 0;
        protected int TicksWithoutMaster = 0;
        private Color originalColor;
        private bool colorChanged = false;
        private bool _feralTriggered = false;


        public void SetSlaveMaster(Pawn master)
        {
            Master = master;
            OriginalFaction = Pawn?.Faction;
            CurrentTick = 0;
            TicksWithoutMaster = 0;
            _feralTriggered = false;

            if (Props.applyColorTint)
                ApplyColor(Pawn, Props.slaveColor);
        }


        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return;

            CurrentTick++;

            if (CurrentTick >= Props.regenerationTicks)
            {
                CurrentTick = 0;
                HandleNeedsAndSuppression(pawn);
            }

            if (Props.goFeralOnMasterDeath && Master != null
                && (Master.Dead || Master.Destroyed))
            {
                if (!Props.masterDeathMessage.NullOrEmpty())
                    Messages.Message(
                        string.Format(Props.masterDeathMessage, pawn.LabelShort),
                        pawn, MessageTypeDefOf.NegativeEvent, false);

                TriggerFeral(pawn);
                return;
            }

            if (Props.checkMasterDistance)
            {
                bool masterInRange = Master != null
                    && Master.Spawned
                    && Master.Map == pawn.Map
                    && Master.Position.DistanceTo(pawn.Position) <= Props.masterMaxDistance;

                if (!masterInRange)
                {
                    TicksWithoutMaster++;

                    if (TicksWithoutMaster == Props.ticksBeforeBerserkWithoutMaster / 2)
                    {
                        if (!Props.masterTooFarMessage.NullOrEmpty())
                            Messages.Message(
                                string.Format(Props.masterTooFarMessage, pawn.LabelShort),
                                pawn, MessageTypeDefOf.CautionInput, false);
                    }

                    if (TicksWithoutMaster >= Props.ticksBeforeBerserkWithoutMaster)
                    {
                        if (!Props.berserkMessage.NullOrEmpty())
                            Messages.Message(
                                string.Format(Props.berserkMessage, pawn.LabelShort),
                                pawn, MessageTypeDefOf.NegativeEvent, false);

                        TriggerFeral(pawn);
                    }
                }
                else
                {
                    TicksWithoutMaster = 0;
                }
            }
        }


        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            Pawn pawn = Pawn;
            if (pawn == null) return;

            if (Props.applyColorTint)
                RestoreColor(pawn);

            if (!_feralTriggered && Props.goFeralOnHediffExpired && pawn.Spawned && !pawn.Dead)
                TriggerFeral(pawn);
        }


        private void HandleNeedsAndSuppression(Pawn pawn)
        {
            if (pawn.needs == null) return;

            foreach (Need need in pawn.needs.AllNeeds)
                need.CurLevel = need.MaxLevel;

            if (Suppression != null && Suppression.CurLevelPercentage <= 0.94f)
                SlaveRebellionUtility.IncrementSuppression(Suppression, pawn, pawn, 5);
        }

        private void TriggerFeral(Pawn pawn)
        {
            if (_feralTriggered) return;
            _feralTriggered = true;

            pawn.guest?.SetGuestStatus(null);

            if (pawn.Faction == Faction.OfPlayer || pawn.Faction == OriginalFaction)
                pawn.SetFaction(null);

            if (parent != null && pawn.health?.hediffSet != null)
            {
                Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(parent.def);
                if (h != null)
                    pawn.health.RemoveHediff(h);
            }

            if (!pawn.Dead && pawn.Spawned)
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.Berserk, null, true, true);
        }

        private void ApplyColor(Pawn pawn, Color color)
        {
            if (pawn?.story == null) return;
            originalColor = pawn.story.SkinColorBase;
            colorChanged = true;
            pawn.story.SkinColorBase = color;
            pawn.story.skinColorOverride = color;
            PortraitsCache.SetDirty(pawn);
        }

        private void RestoreColor(Pawn pawn)
        {
            if (!colorChanged || pawn?.story == null) return;
            pawn.story.SkinColorBase = originalColor;
            pawn.story.skinColorOverride = null;
            colorChanged = false;
            PortraitsCache.SetDirty(pawn);
        }


        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref Master, "pawnSlaveMaster");
            Scribe_References.Look(ref OriginalFaction, "slaveOriginalFaction");
            Scribe_Values.Look(ref CurrentTick, "slaveCurrentRegenTick", 0);
            Scribe_Values.Look(ref TicksWithoutMaster, "slaveTicksWithoutMaster", 0);
            Scribe_Values.Look(ref originalColor, "slaveOriginalColor", Color.white);
            Scribe_Values.Look(ref colorChanged, "slaveColorChanged", false);
            Scribe_Values.Look(ref _feralTriggered, "feralTriggered", false);
        }
    }
}