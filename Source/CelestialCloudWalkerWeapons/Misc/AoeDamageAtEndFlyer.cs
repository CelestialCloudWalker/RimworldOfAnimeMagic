using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class AoeDamageAtEndFlyer : PawnFlyer
    {
        public float Radius = 4f;
        public DamageDef DamageDef = DamageDefOf.Burn;
        public float Damage;

        protected override void RespawnPawn()
        {
            IEnumerable<Pawn> Targets = AnimeArsenalUtility.GetEnemyPawnsInRange(this.Position, this.MapHeld, this.Radius);
            foreach (var t in Targets)
            {
                if (!t.Destroyed && !t.Dead)
                {
                    t.TakeDamage(new DamageInfo(DamageDef, Damage));
                }
            }

            base.RespawnPawn();
        }
    }
}