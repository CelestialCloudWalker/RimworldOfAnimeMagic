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