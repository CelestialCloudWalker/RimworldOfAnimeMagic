using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class ScalingStatDamageProjectile : Bullet
    {
        public override int DamageAmount
        {
            get
            {
                return this.def.projectile.GetDamageAmount(DamageStat, null);
            }
        }

        public float DamageStat = 1f;

        public void SetDamageScale(float Value)
        {
            DamageStat = Value;
            Log.Message($"Setting Damage Scale to {Value} DamageAMount {DamageAmount}");
        }
    }
}

