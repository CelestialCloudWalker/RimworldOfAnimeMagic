using RimWorld;
using System;
using Verse;

namespace AnimeArsenal
{
    public class DelegateFlyer : PawnFlyer
    {
        public event Action<PawnFlyer> OnSpawn;
        public event Action<Pawn, PawnFlyer> OnRespawnPawn;
        private bool respawnQueued = false;

        public override void PostMake()
        {
            base.PostMake();
            try
            {
                LongEventHandler.ExecuteWhenFinished(() => OnSpawn?.Invoke(this));
            }
            catch (Exception ex)
            {
                Log.Error($"DelegateFlyer - Error in PostMake: {ex}");
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (respawnQueued)
            {
                respawnQueued = false;
                try
                {
                    OnRespawnPawn?.Invoke(FlyingPawn, this);
                }
                catch (Exception ex)
                {
                    Log.Error($"DelegateFlyer - Error in queued respawn: {ex}");
                }
            }
        }

        protected override void RespawnPawn()
        {
            if (FlyingPawn == null)
            {
                Log.Error("DelegateFlyer - FlyingPawn is null during RespawnPawn");
                return;
            }

            try
            {
                base.RespawnPawn();
                respawnQueued = true;
            }
            catch (Exception ex)
            {
                Log.Error($"DelegateFlyer - Error in RespawnPawn: {ex}");
            }
        }
    }
}