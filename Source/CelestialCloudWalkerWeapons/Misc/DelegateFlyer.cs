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
            Map map = this.Map;

            base.RespawnPawn();

            if (pawn != null && map != null)
            {
                OnRespawnPawn?.Invoke(pawn, this, map);
            }
        }
    }
}