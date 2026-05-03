using EMF;
using RimWorld;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ManifestWeapon : CompProperties_AbilityEffect
    {
        public ThingDef weaponDef;
        public ThingDef weaponStuff;
        public QualityCategory weaponQuality = QualityCategory.Normal;
        public HediffDef boundWeaponHediff;

        public CompProperties_ManifestWeapon()
        {
            compClass = typeof(CompAbilityEffect_ManifestWeapon);
        }
    }

    public class CompAbilityEffect_ManifestWeapon : CompAbilityEffect
    {
        public new CompProperties_ManifestWeapon Props => (CompProperties_ManifestWeapon)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn == null || Props.weaponDef == null) return;

            if (Props.boundWeaponHediff != null)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.boundWeaponHediff);
                if (hediff != null)
                {
                    EMF.HediffComp_BoundWeapon boundComp = hediff.TryGetComp<EMF.HediffComp_BoundWeapon>();
                    if (boundComp != null && boundComp.HasBoundThing && !boundComp.BoundThing.Destroyed)
                    {
                        boundComp.SummonWeapon();
                        return;
                    }
                }
            }

            ThingDef stuff = Props.weaponStuff;
            Thing weapon = ThingMaker.MakeThing(Props.weaponDef, stuff);

            if (weapon.TryGetComp<CompQuality>() is CompQuality qual)
                qual.SetQuality(Props.weaponQuality, ArtGenerationContext.Colony);

            if (weapon is ThingWithComps weaponComps && pawn.equipment != null)
            {
                if (pawn.equipment.Primary != null)
                {
                    pawn.equipment.TryDropEquipment(
                        pawn.equipment.Primary,
                        out _,
                        pawn.Position);
                }

                pawn.equipment.DropAndEquip(weaponComps);

                if (Props.boundWeaponHediff != null)
                {
                    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.boundWeaponHediff);

                    if (hediff == null)
                    {
                        hediff = HediffMaker.MakeHediff(Props.boundWeaponHediff, pawn);
                        pawn.health.AddHediff(hediff);
                    }

                    EMF.HediffComp_BoundWeapon boundComp = hediff.TryGetComp<EMF.HediffComp_BoundWeapon>();
                    boundComp?.SetBoundWeapon(weapon);
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent.pawn != null && !parent.pawn.Dead;
        }
    }
}