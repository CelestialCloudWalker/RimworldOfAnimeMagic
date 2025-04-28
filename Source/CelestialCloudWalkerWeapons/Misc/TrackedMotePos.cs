using AnimeArsenal;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class TrackedMotePos
    {
        public MoteDualAttached Mote;
        public int RemainingTicks;
        public IntVec3 SourcePos;
        public IntVec3 TargetPos;

        public TrackedMotePos(MoteDualAttached mote, IntVec3 sourcePos, IntVec3 targetPos, int lifetimeTicks)
        {
            Mote = mote;
            SourcePos = sourcePos;
            TargetPos = targetPos;
            RemainingTicks = lifetimeTicks;
        }
    }
    public class DashTrailEffect
    {
        private readonly Map map;
        private readonly List<TrackedMotePos> activeTrails = new List<TrackedMotePos>();
        private static readonly int TrailLifetime = 120;
        private static readonly ThingDef TrailMoteDef = DefDatabase<ThingDef>.GetNamed("Mote_GraserBeamBase");

        public DashTrailEffect(Map map)
        {
            this.map = map;
        }

        public void CreateTrailBetween(IntVec3 source, IntVec3 target)
        {
            if (!source.IsValid || !target.IsValid || map == null)
                return;

            if (TrailMoteDef == null)
            {
                Log.Error("DashTrailEffect: Trail mote def not found");
                return;
            }

            MoteDualAttached mote = (MoteDualAttached)ThingMaker.MakeThing(TrailMoteDef);
            if (mote == null) return;

            GenSpawn.Spawn(mote, source, map);
            mote.Attach(new TargetInfo(source, map, false), new TargetInfo(target, map, false));

            TrackedMotePos trackedMote = new TrackedMotePos(mote, source, target, TrailLifetime);
            activeTrails.Add(trackedMote);
        }

        public void Tick()
        {
            for (int i = activeTrails.Count - 1; i >= 0; i--)
            {
                TrackedMotePos trail = activeTrails[i];
                trail.RemainingTicks--;

                if (trail.RemainingTicks <= 0 || trail.Mote == null || trail.Mote.Destroyed)
                {
                    if (trail.Mote != null && !trail.Mote.Destroyed)
                    {
                        trail.Mote.Destroy();
                    }
                    activeTrails.RemoveAt(i);
                }
                else
                {
                    trail.Mote.Maintain();
                }
            }
        }

        public void Clear()
        {
            foreach (TrackedMotePos trail in activeTrails)
            {
                if (trail.Mote != null && !trail.Mote.Destroyed)
                {
                    trail.Mote.Destroy();
                }
            }
            activeTrails.Clear();
        }
    }
    public class DashBehaviour : IExposable
    {
        protected Pawn pawn;
        protected Map map;
        protected IntVec3 initialPosition;
        protected IntVec3 targetPosition;
        protected IntVec3 lastPosition;

        protected List<IntVec3> dashPath = new List<IntVec3>();
        protected int currentPathIndex = 0;

        protected int maxJumps = 4;
        protected int jumpDistance = 5;
        protected int delayBetweenJumps = 15;
        protected int actionDuration = 30;

        protected List<IntVec3> jumpOffsets = new List<IntVec3>();
        protected DashTrailEffect trailEffect;

        protected Action<IntVec3> onJumpStart;
        protected Action<IntVec3> onJumpComplete;
        protected Action onDashComplete;

        protected int currentTick = 0;
        protected bool isRunning = false;
        protected bool isFinished = false;
        protected int jumpDelayCounter = 0;

        protected bool isDashing = false;
        protected bool isPerformingAction = false;
        protected bool isFlyerActive = false;

        public DashBehaviour() { }

        public DashBehaviour(Pawn pawn, IntVec3 target)
        {
            this.pawn = pawn;
            this.map = pawn.Map;
            this.initialPosition = pawn.Position;
            this.targetPosition = target;
            this.lastPosition = pawn.Position;
            this.trailEffect = new DashTrailEffect(map);
        }

        public bool IsRunning => isRunning;
        public bool IsFinished => isFinished;

        public void Initialize(
            int maxJumps = 4,
            int jumpDistance = 5,
            int delayBetweenJumps = 15,
            int actionDuration = 30,
            Action<IntVec3> onJumpStart = null,
            Action<IntVec3> onJumpComplete = null,
            Action onDashComplete = null,
            List<IntVec3> customJumpOffsets = null)
        {
            this.maxJumps = maxJumps;
            this.jumpDistance = jumpDistance;
            this.delayBetweenJumps = delayBetweenJumps;
            this.actionDuration = actionDuration;
            this.onJumpStart = onJumpStart;
            this.onJumpComplete = onJumpComplete;
            this.onDashComplete = onDashComplete;

            if (customJumpOffsets != null && customJumpOffsets.Count > 0)
            {
                this.jumpOffsets = new List<IntVec3>(customJumpOffsets);
            }
            else
            {
                this.jumpOffsets = GenerateDefaultOffsets();
            }
        }

        private List<IntVec3> GenerateDefaultOffsets()
        {
            List<IntVec3> defaultOffsets = new List<IntVec3>();

            for (int i = 0; i < maxJumps; i++)
            {
                IntVec3 offset = new IntVec3(
                    (i % 2 == 0 ? 1 : -1) * jumpDistance,
                    0,
                    (i % 4 < 2 ? 1 : -1) * jumpDistance
                );

                defaultOffsets.Add(offset);
            }

            return defaultOffsets;
        }

        public void Start()
        {
            isRunning = true;
            isFinished = false;
            isFlyerActive = false;
            isDashing = false;
            isPerformingAction = false;
            lastPosition = pawn.Position;

            GenerateDashPath();
            if (dashPath.Count > 0)
            {
                isDashing = true;
                currentPathIndex = 0;
            }
            else
            {
                Stop();
            }
        }

        public void Stop()
        {
            isRunning = false;
            isFinished = true;
            trailEffect.Clear();
            onDashComplete?.Invoke();
        }

        public void Tick()
        {
            if (!isRunning)
                return;

            currentTick++;
            trailEffect.Tick();

            if (isDashing)
            {
                TickDashing();
            }
            else if (isPerformingAction)
            {
                TickAction();
            }
            else
            {
                Stop();
            }
        }

        protected enum EightWayDirection
        {
            North = 0,
            Northeast = 1,
            East = 2,
            Southeast = 3,
            South = 4,
            Southwest = 5,
            West = 6,
            Northwest = 7
        }

        protected virtual void GenerateDashPath()
        {
            dashPath.Clear();
            dashPath.Add(targetPosition);
            IntVec3 lastPos = targetPosition;

            int jumpsToExecute = Math.Min(maxJumps, jumpOffsets.Count);

            for (int i = 0; i < jumpsToExecute; i++)
            {
                IntVec3 offset = RotateOffsetByFacing(jumpOffsets[i]);

                IntVec3 nextPos = lastPos + offset;
                nextPos = EnsurePositionIsValid(nextPos, map);

                dashPath.Add(nextPos);
                lastPos = nextPos;
            }
        }

        protected virtual EightWayDirection GetEightWayDirection(IntVec3 vector)
        {
            if (vector.x == 0 && vector.z == 0)
                return EightWayDirection.North;

            float angle = Mathf.Atan2(vector.z, vector.x) * Mathf.Rad2Deg;

            if (angle < 0)
                angle += 360f;

            if (angle >= 337.5f || angle < 22.5f)
                return EightWayDirection.East;
            else if (angle >= 22.5f && angle < 67.5f)
                return EightWayDirection.Northeast;
            else if (angle >= 67.5f && angle < 112.5f)
                return EightWayDirection.North;
            else if (angle >= 112.5f && angle < 157.5f)
                return EightWayDirection.Northwest;
            else if (angle >= 157.5f && angle < 202.5f)
                return EightWayDirection.West;
            else if (angle >= 202.5f && angle < 247.5f)
                return EightWayDirection.Southwest;
            else if (angle >= 247.5f && angle < 292.5f)
                return EightWayDirection.South;
            else
                return EightWayDirection.Southeast;
        }

        protected virtual IntVec3 RotateOffsetByFacing(IntVec3 offset)
        {
            IntVec3 directionVector = targetPosition - initialPosition;

            EightWayDirection eightWayDir = GetEightWayDirection(directionVector);

            switch (eightWayDir)
            {
                case EightWayDirection.North:
                    return offset;

                case EightWayDirection.Northeast:
                    return RotateVectorBy45Degrees(offset, 1);

                case EightWayDirection.East:
                    return new IntVec3(offset.z, 0, -offset.x);

                case EightWayDirection.Southeast:
                    return RotateVectorBy45Degrees(new IntVec3(offset.z, 0, -offset.x), 1);

                case EightWayDirection.South:
                    return new IntVec3(-offset.x, 0, -offset.z);

                case EightWayDirection.Southwest:
                    return RotateVectorBy45Degrees(new IntVec3(-offset.x, 0, -offset.z), 1);

                case EightWayDirection.West:
                    return new IntVec3(-offset.z, 0, offset.x);

                case EightWayDirection.Northwest:
                    return RotateVectorBy45Degrees(new IntVec3(-offset.z, 0, offset.x), 1);

                default:
                    return offset;
            }
        }

        protected virtual IntVec3 RotateVectorBy45Degrees(IntVec3 vector, int times)
        {
            for (int i = 0; i < times; i++)
            {
                int newX = (int)Math.Round((vector.x - vector.z) / 1.414f);
                int newZ = (int)Math.Round((vector.x + vector.z) / 1.414f);
                vector = new IntVec3(newX, 0, newZ);
            }

            return vector;
        }

        protected virtual void TickDashing()
        {
            if (isFlyerActive)
                return;

            if (jumpDelayCounter > 0)
            {
                jumpDelayCounter--;
                return;
            }

            if (currentPathIndex < dashPath.Count)
            {
                ExecuteNextJump();
            }
            else
            {
                isDashing = false;
                isPerformingAction = true;
                currentTick = 0;
            }
        }

        protected virtual void ExecuteNextJump()
        {
            if (isFlyerActive || pawn.GetAttachment(ThingDefOf.PawnFlyer) != null)
            {
                return;
            }

            IntVec3 nextPosition = dashPath[currentPathIndex];
            onJumpStart?.Invoke(nextPosition);
            isFlyerActive = true;
            CreateFlyerToPosition(pawn.Position, nextPosition);
        }

        protected virtual void CreateFlyerToPosition(IntVec3 start, IntVec3 target)
        {
            DelegateFlyer pawnFlyer = (DelegateFlyer)PawnFlyer.MakeFlyer(
                CelestialDefof.AnimeArsenal_DelegateFlyer,
                pawn,
                target,
                null,
                null
            );
            pawnFlyer.OnRespawnPawn += OnFlyerLand;
            GenSpawn.Spawn(pawnFlyer, start, map);

            // Create trail from last position to current position
            trailEffect.CreateTrailBetween(lastPosition, target);
        }

        protected virtual void OnFlyerLand(Pawn pawn, PawnFlyer flyer, Map map)
        {
            if (flyer is DelegateFlyer delegateFlyer)
            {
                delegateFlyer.OnRespawnPawn -= OnFlyerLand;
            }

            // Update last position
            lastPosition = pawn.Position;

            isFlyerActive = false;
            onJumpComplete?.Invoke(pawn.Position);
            currentPathIndex++;
            jumpDelayCounter = delayBetweenJumps;
        }

        protected virtual void TickAction()
        {
            if (currentTick >= actionDuration)
            {
                isPerformingAction = false;
                Stop();
            }
        }

        protected virtual IntVec3 EnsurePositionIsValid(IntVec3 position, Map map)
        {
            position.x = Mathf.Clamp(position.x, 0, map.Size.x - 1);
            position.z = Mathf.Clamp(position.z, 0, map.Size.z - 1);
            if (!position.Walkable(map))
            {
                IntVec3 fallbackPosition = CellFinder.StandableCellNear(position, map, jumpDistance / 2);
                if (fallbackPosition.IsValid)
                {
                    return fallbackPosition;
                }
            }

            return position;
        }

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref initialPosition, "initialPosition");
            Scribe_Values.Look(ref targetPosition, "targetPosition");
            Scribe_Values.Look(ref lastPosition, "lastPosition");
            Scribe_Values.Look(ref currentPathIndex, "currentPathIndex");
            Scribe_Values.Look(ref currentTick, "currentTick");
            Scribe_Values.Look(ref isRunning, "isRunning");
            Scribe_Values.Look(ref isFinished, "isFinished");
            Scribe_Values.Look(ref jumpDelayCounter, "jumpDelayCounter");
            Scribe_Values.Look(ref isDashing, "isDashing");
            Scribe_Values.Look(ref isPerformingAction, "isPerformingAction");
            Scribe_Values.Look(ref isFlyerActive, "isFlyerActive");

            Scribe_Collections.Look(ref jumpOffsets, "jumpOffsets", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && jumpOffsets == null)
            {
                jumpOffsets = new List<IntVec3>();
            }

            Scribe_Collections.Look(ref dashPath, "dashPath", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && dashPath == null)
            {
                dashPath = new List<IntVec3>();
            }
        }
    }
}