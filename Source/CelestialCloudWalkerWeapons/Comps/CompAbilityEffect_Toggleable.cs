using RimWorld;
using Verse;

namespace AnimeArsenal
{
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
                if (CanStart())
                {
                    IsActive = true;
                    OnToggleOn();
                }
            }
        }

        public abstract bool CanStart();
    }
}