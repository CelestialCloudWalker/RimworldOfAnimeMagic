using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class Verb_ThrowAntiDemonBomb : Verb_LaunchProjectile
    {
        protected override bool TryCastShot()
        {
            Pawn caster = CasterPawn;
            if (caster == null) return false;

            ThingWithComps equipment = caster.equipment?.Primary;
            if (equipment == null || equipment.stackCount <= 0) return false;

            bool shot = base.TryCastShot();

            if (shot)
            {
                equipment.stackCount--;
                if (equipment.stackCount <= 0)
                    caster.equipment.Remove(equipment);
            }

            return shot;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return base.ValidateTarget(target, showMessages);
        }
    }
}