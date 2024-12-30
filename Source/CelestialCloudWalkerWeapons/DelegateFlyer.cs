using RimWorld;
using System;
using Verse;

namespace AnimeArsenal
{
    public class DelegateFlyer : PawnFlyer
    {
        public event Action<PawnFlyer> OnSpawn;
        public event Action<Pawn, PawnFlyer> OnRespawnPawn;


        public override void PostMake()
        {
            base.PostMake();
            OnSpawn?.Invoke(this);
        }

        protected override void RespawnPawn()
        {
            base.RespawnPawn();
            OnRespawnPawn?.Invoke(this.FlyingPawn, this);
        }
    }
}