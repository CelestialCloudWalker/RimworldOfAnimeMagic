using System.Reflection;
using Verse;

namespace AnimeArsenal
{
    public abstract class ThingCompExt : ThingComp
    {

        private static readonly PropertyInfo holderPropertyInfo = typeof(CompEquippable).GetProperty("Holder", BindingFlags.Instance | BindingFlags.NonPublic);

        protected Pawn _EquipOwner = null;

        protected Pawn EquipOwner
        {
            get
            {
                if (_EquipOwner == null)
                {
                    if (parent is ThingWithComps equipment)
                    {
                        CompEquippable compEquippable = equipment.GetComp<CompEquippable>();
                        if (compEquippable != null)
                        {
                            _EquipOwner = holderPropertyInfo.GetValue(compEquippable) as Pawn;
                        }
                    }
                }

                return _EquipOwner;
            }
        }

        protected bool IsEquipped = false;


        public virtual DamageWorker.DamageResult Notify_ApplyMeleeDamageToTarget(LocalTargetInfo target, DamageWorker.DamageResult DamageWorkerResult)
        {
            return DamageWorkerResult;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            _EquipOwner = pawn;
            IsEquipped = true;
        }


        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            _EquipOwner = null;
            IsEquipped = false;
        }

        public virtual void Notify_EquipOwnerUsedVerb(Pawn pawn, Verb verb)
        {

        }

        public virtual void ModifyDamageInfo(ref DamageInfo dinfo)
        {

        }
    }
}