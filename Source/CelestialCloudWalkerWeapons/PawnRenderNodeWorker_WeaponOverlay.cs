using AnimeArsenal;
using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class PawnRenderNodeWorker_WeaponOverlay : PawnRenderNodeWorker
    {
        [TweakValue("EMO")]
        public static float TWEAK_OFFSET = 2.921543f;

        private static readonly Color BaseRottenColor = new Color(0.29f, 0.25f, 0.22f);
        public static readonly Color DessicatedColorInsect = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Vector3 BaseCarriedOffset = new Vector3(0f, 0f, -0.1f);
        private static readonly Vector3 EqLocNorth = new Vector3(0f, 0f, -0.11f);
        private static readonly Vector3 EqLocEast = new Vector3(0.22f, 0f, -0.22f);
        private static readonly Vector3 EqLocSouth = new Vector3(0f, 0f, -0.22f);
        private static readonly Vector3 EqLocWest = new Vector3(-0.22f, 0f, -0.22f);
        public const float Layer_Carried = 90f;
        public const float Layer_Carried_Behind = -10f;

        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms) || parms.Portrait || parms.pawn.Dead || !parms.pawn.Spawned)
                return false;

            Pawn pawn = parms.pawn;
            if (pawn == null)
            {
                return false;
            }

            if (pawn.Rotation == Rot4.North)
            {
                return false;
            }

            List<HediffComp_Overlay> overlays = GetValidOverlayHediffs(pawn);
            if (overlays.Count == 0)
            {
                return false;
            }

            return pawn.equipment != null && pawn.equipment.Primary != null;
        }

        public static List<HediffComp_Overlay> GetValidOverlayHediffs(Pawn pawn)
        {
            List<HediffComp_Overlay> result = new List<HediffComp_Overlay>();
            List<HediffComp_Overlay> allOverlays = pawn.health.hediffSet.GetHediffComps<HediffComp_Overlay>().ToList();

            if (allOverlays != null && allOverlays.Count > 0)
            {
                foreach (HediffComp_Overlay overlay in allOverlays)
                {
                    if (overlay != null && overlay.Props.overlayGraphic != null && overlay.showOverlay)
                    {
                        result.Add(overlay);
                    }
                }
            }

            return result;
        }

        public override void AppendDrawRequests(PawnRenderNode node, PawnDrawParms parms, List<PawnGraphicDrawRequest> requests)
        {
            requests.Add(new PawnGraphicDrawRequest(node, null, null));
        }
        public override MaterialPropertyBlock GetMaterialPropertyBlock(PawnRenderNode node, Material material, PawnDrawParms parms)
        {
            return base.GetMaterialPropertyBlock(node, material, parms);
        }


        public override void PostDraw(PawnRenderNode node, PawnDrawParms parms, Mesh mesh, Matrix4x4 matrix)
        {
            if (parms.pawn == null || parms.pawn.equipment == null || parms.pawn.equipment.Primary == null)
            {
                return;
            }

            Thing weapon = parms.pawn.equipment.Primary;
            Vector3 drawPos = parms.matrix.Position();

            List<HediffComp_Overlay> overlays = GetValidOverlayHediffs(parms.pawn);
            if (overlays.Count == 0)
            {
                return;
            }

            float aimAngle = 0f;
            Job curJob = parms.pawn.CurJob;
            bool showWeapon = curJob == null ||
                             (curJob.def != null && !curJob.def.neverShowWeapon);
            if (!showWeapon)
                return;

            float equipmentDrawDistanceFactor = parms.pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;

            Pawn_StanceTracker stances = parms.pawn.stances;
            Stance_Busy stance_Busy = ((stances != null) ? stances.curStance : null) as Stance_Busy;

            if (!parms.flags.HasFlag(PawnRenderFlags.NeverAimWeapon) &&
                stance_Busy != null && !stance_Busy.neverAimWeapon && stance_Busy.focusTarg.IsValid)
            {
                Vector3 targetPos = stance_Busy.focusTarg.HasThing
                    ? stance_Busy.focusTarg.Thing.DrawPos
                    : stance_Busy.focusTarg.Cell.ToVector3Shifted();

                if ((targetPos - parms.pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
                {
                    aimAngle = (targetPos - parms.pawn.DrawPos).AngleFlat();
                }

                Verb currentEffectiveVerb = parms.pawn.CurrentEffectiveVerb;
                if (currentEffectiveVerb?.AimAngleOverride != null)
                {
                    aimAngle = currentEffectiveVerb.AimAngleOverride.Value;
                }

                drawPos += new Vector3(0f, 0f, 0.4f + weapon.def.equippedDistanceOffset)
                    .RotatedBy(aimAngle) * equipmentDrawDistanceFactor;
            }
            else if (PawnRenderUtility.CarryWeaponOpenly(parms.pawn))
            {
                aimAngle = 143f;
                switch (parms.facing.AsInt)
                {
                    case 0: // North
                        drawPos += EqLocNorth * equipmentDrawDistanceFactor;
                        break;
                    case 1: // East
                        drawPos += EqLocEast * equipmentDrawDistanceFactor;
                        break;
                    case 2: // South
                        drawPos += EqLocSouth * equipmentDrawDistanceFactor;
                        break;
                    case 3: // West
                        drawPos += EqLocWest * equipmentDrawDistanceFactor;
                        aimAngle = 217f;
                        break;
                }
            }
            else
            {
                return;
            }

            float rotationAngle = aimAngle - 90f;
            Mesh overlayMesh;

            if (aimAngle > 20f && aimAngle < 160f)
            {
                overlayMesh = MeshPool.plane10;
                rotationAngle += weapon.def.equippedAngleOffset;
            }
            else if (aimAngle > 200f && aimAngle < 340f)
            {
                overlayMesh = MeshPool.plane10Flip;
                rotationAngle -= 180f;
                rotationAngle -= weapon.def.equippedAngleOffset;
            }
            else
            {
                overlayMesh = MeshPool.plane10;
                rotationAngle += weapon.def.equippedAngleOffset;
            }

            rotationAngle %= 360f;

            CompEquippable compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable != null)
            {
                Vector3 recoilVector;
                float recoilAngle;
                EquipmentUtility.Recoil(weapon.def, EquipmentUtility.GetRecoilVerb(compEquippable.AllVerbs),
                    out recoilVector, out recoilAngle, aimAngle);
                drawPos += recoilVector;
                rotationAngle += recoilAngle;
            }

            Vector3 baseDrawPos = drawPos;
            Vector3 scale = new Vector3(weapon.Graphic.drawSize.x, 0f, weapon.Graphic.drawSize.y);

            Vector3 weaponOffset = Vector3.zero;
            if (weapon.def.HasModExtension<DrawOffsetExt>())
            {
                weaponOffset = weapon.def.GetModExtension<DrawOffsetExt>().GetOffsetForRot(parms.pawn.Rotation) * TWEAK_OFFSET;
            }

            // Draw all valid overlays
            foreach (HediffComp_Overlay overlay in overlays)
            {
                if (overlay.Props.overlayGraphic == null || overlay.Props.overlayGraphic.Graphic == null)
                    continue;

                Vector3 currentDrawPos = baseDrawPos;

                currentDrawPos.y = overlay.Props.altitudeLayer.AltitudeFor();
                currentDrawPos += weaponOffset;

                Graphic graphic = overlay.Props.overlayGraphic.Graphic;
                Material mat = graphic.MatAt(parms.pawn.Rotation);

                if (overlay.MaskTex != null)
                {
                    mat.SetTexture("_MaskTex", overlay.MaskTex);

                    Matrix4x4 overlayMatrix = Matrix4x4.TRS(currentDrawPos, Quaternion.AngleAxis(rotationAngle, Vector3.up), scale);
                    Graphics.DrawMesh(overlayMesh, overlayMatrix, mat, 0, null, 0);
                }
                else
                {
                    Matrix4x4 overlayMatrix = Matrix4x4.TRS(currentDrawPos, Quaternion.AngleAxis(rotationAngle, Vector3.up), scale);
                    Graphics.DrawMesh(overlayMesh, overlayMatrix, mat, 0);
                }
            }
        }
    }
}