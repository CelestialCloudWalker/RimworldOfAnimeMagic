using RimWorld;
using System;
using Verse;

namespace AnimeArsenal
{
    public class DelegateFlyer : PawnFlyer
    {
        public event Action<Pawn, PawnFlyer, Map> OnRespawnPawn;

        protected override void RespawnPawn()
        {
            Pawn pawn = this.FlyingPawn;
            base.RespawnPawn();
            OnRespawnPawn?.Invoke(pawn, this, pawn.Map);
        }
    }
}