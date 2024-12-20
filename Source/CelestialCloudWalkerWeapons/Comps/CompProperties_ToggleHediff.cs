using RimWorld;
using Verse;

namespace AnimeArsenal
{


    //abstract class just handles basic toggle behaviour but lets the subclass (or child classes) handle what happens when its actually Toggled to On or Off
    public abstract class CompAbilityEffect_Toggleable : CompAbilityEffect
    {
        protected bool IsActive = false;

        public abstract void OnToggleOn();
        public abstract void OnToggleOff();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (IsActive)
            {
                IsActive = false;
                OnToggleOff();
            }
            else
            {
                IsActive = true;
                OnToggleOn();
            }
        }
    }

    public class CompProperties_ToggleHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;

        public CompProperties_ToggleHediff()
        {
            compClass = typeof(CompAbilityEffect_ToggleHediff);
        }
    }

    //this class we are extending form our abstract class 
    public class CompAbilityEffect_ToggleHediff : CompAbilityEffect_Toggleable
    {
        public new CompProperties_ToggleHediff Props => (CompProperties_ToggleHediff)props;

        public override void OnToggleOff()
        {
            Hediff existingHediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            Pawn pawn = this.parent.pawn;
            if (existingHediff != null)
            {
                pawn.health.RemoveHediff(existingHediff);
                Messages.Message("Removed " + Props.hediffDef.label + " from " + pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void OnToggleOn()
        {
            Pawn pawn = this.parent.pawn;
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
            pawn.health.AddHediff(hediff);
            Messages.Message("Added " + Props.hediffDef.label + " to " + pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }
    }


    public class CompProperties_ToggleEnchant : CompProperties_AbilityEffect
    {
        public EnchantDef enchantDef;

        public CompProperties_ToggleEnchant()
        {
            compClass = typeof(CompAbilityEffect_ToggleEnchant);
        }
    }

    public class CompAbilityEffect_ToggleEnchant : CompAbilityEffect_Toggleable
    {
        new CompProperties_ToggleEnchant Props => (CompProperties_ToggleEnchant)props;

        private int resourceCostTimer = 0;
        private int timeUntilExhaustedTimer = 0;          
        private bool isExhausted = false;                
        private int exhaustionCooldownRemaining = 0;     
        private int exhaustionHediffTimer = 0;            

        public override void OnToggleOff()
        {
            RemoveHediff(this.parent.pawn);
        }

        public override void OnToggleOn()
        {
            if (CanStart())
            {
                ApplyHediff(this.parent.pawn);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsActive)
            {
                resourceCostTimer += 1;

                if (resourceCostTimer >= Props.enchantDef.ticksBetweenCost)
                {
                    if (ShouldCancel())
                    {
                        this.OnToggleOff();
                    }

                    resourceCostTimer = 0;
                }

                TickActiveExhaustion();
            }
            else
            {
                TickExhausted();
                ReduceExhaustionBuildup();
            }
        }

        private void TickExhausted()
        {
            if (isExhausted)
            {
                exhaustionCooldownRemaining--;

                if (exhaustionCooldownRemaining <= 0)
                {
                    OnExhaustionEnded();
   
                }
            }
        }

        private void ReduceExhaustionBuildup()
        {
            if (timeUntilExhaustedTimer > 0)
            {
                timeUntilExhaustedTimer--;
            }

            if (exhaustionHediffTimer > 0)
            {
                exhaustionHediffTimer--;
            }
        }

        private void TickActiveExhaustion()
        {
            timeUntilExhaustedTimer++;

            if (timeUntilExhaustedTimer >= Props.enchantDef.ticksBeforeExhaustionStart)
            {
                timeUntilExhaustedTimer = 0;
                OnExhaustionStarted();
            }

            exhaustionHediffTimer++;

            if (exhaustionHediffTimer >= Props.enchantDef.ticksPerExhaustionIncrease)
            {
                if (Props.enchantDef.exhaustionHediff != null)
                {
                    Hediff hediff = this.parent.pawn.health.GetOrAddHediff(Props.enchantDef.exhaustionHediff);

                    if (hediff != null)
                    {
                        hediff.Severity += Props.enchantDef.exhaustionPerTick;
                    }
                }

                exhaustionHediffTimer = 0;
            }
        }

        private void OnExhaustionStarted()
        {
            isExhausted = true;
            exhaustionCooldownRemaining = Props.enchantDef.exhausationCooldownTicks;
        }

        private void OnExhaustionEnded()
        {
            exhaustionCooldownRemaining = 0;
            isExhausted = false;
        }

        private Resource_Gene GetResourcGene(Pawn Pawn)
        {
            if (Props.enchantDef.resourceGene == null)
            {
                return null;
            }

            Resource_Gene resource_Gene = (Resource_Gene)Pawn.genes.GetGene(Props.enchantDef.resourceGene);
            if (resource_Gene != null)
            {
                return resource_Gene;
            }
            return null;
        }

        private bool CanStart()
        {
            if (Props.enchantDef == null)
            {
                return false;
            }

            var rsourceGene = GetResourcGene(this.parent.pawn);

            if (rsourceGene != null)
            {
                return Props.enchantDef.resourceCostPerTick > 0 && rsourceGene.HasAstralPulse(GetChannelCost());
            }
            return true;
        }

        private float GetChannelCost()
        {
            return Props.enchantDef != null ? Props.enchantDef.resourceCostPerTick * (isExhausted ? 2f : 1f) : 5f;
        }

        private bool ShouldCancel()
        {
            var rsourceGene = GetResourcGene(this.parent.pawn);

            if (rsourceGene != null)
            {
                return Props.enchantDef.resourceCostPerTick > 0 && !rsourceGene.HasAstralPulse(GetChannelCost());
            }

            return false;
        }

        private void ApplyHediff(Pawn Pawn)
        {
            Pawn.health.GetOrAddHediff(Props.enchantDef.enchantHediff);
            Messages.Message("Added " + Props.enchantDef.enchantHediff.label + " to " + Pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }

        private void RemoveHediff(Pawn Pawn)
        {
            Hediff existingHediff = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.enchantDef.enchantHediff);

            if (existingHediff != null)
            {
                Pawn.health.RemoveHediff(existingHediff);
                Messages.Message("Removed " + Props.enchantDef.enchantHediff.label + " from " + Pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref resourceCostTimer, "resourceCostTimer", 0);
            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref isExhausted, "isExhausted", false);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref exhaustionHediffTimer, "exhaustionHediffTimer", 0);
        }
    }
}