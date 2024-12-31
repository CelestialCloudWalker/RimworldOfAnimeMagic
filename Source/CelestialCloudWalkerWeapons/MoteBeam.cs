using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class MoteBeam : Mote
    {
        private Vector3 origin;
        private Vector3 destination;
        private Vector3 originOffset = Vector3.zero;
        private Material beamMat;
        private Mesh beamMesh;
        private float beamWidth = 6f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref destination, "destination");
        }

        public void UpdateTargets(Vector3 newOrigin, Vector3 newDestination, float beamWidth, Vector3 originOffset = default(Vector3))
        {
            // Adjust the origin to the center of the cell
            origin = newOrigin.Yto0() + new Vector3(0, 0, 0);
            destination = newDestination;
            if (originOffset != default(Vector3))
            {
                this.originOffset = originOffset;
            }
            this.beamWidth = beamWidth;
            UpdateMesh();
        }

        private void UpdateMesh()
        {
            if (beamMesh == null)
                beamMesh = new Mesh();

            Vector3 direction = (destination - origin).normalized;
            Vector3 cross = Vector3.Cross(direction, Vector3.up).normalized;
            if (cross == Vector3.zero)
                cross = Vector3.Cross(direction, Vector3.forward).normalized;

            float length = Vector3.Distance(origin, destination);

            Vector3[] vertices = new Vector3[4];
            vertices[0] = Vector3.zero - cross * beamWidth / 2f;
            vertices[1] = Vector3.zero + cross * beamWidth / 2f;
            vertices[2] = direction * length - cross * beamWidth / 2f;
            vertices[3] = direction * length + cross * beamWidth / 2f;

            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(0, 1);
            uv[2] = new Vector2(1, 0);
            uv[3] = new Vector2(1, 1);

            int[] triangles = new int[] { 0, 1, 2, 2, 1, 3 };

            beamMesh.Clear();
            beamMesh.vertices = vertices;
            beamMesh.uv = uv;
            beamMesh.triangles = triangles;
            beamMesh.RecalculateNormals();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (beamMat == null)
            {
                beamMat = MaterialPool.MatFrom(def.graphicData.texPath, def.graphicData.shaderType.Shader, def.graphicData.color, def.graphicData.renderQueue);
            }

            if (beamMesh == null)
                UpdateMesh();

            // Use the adjusted origin directly
            Graphics.DrawMesh(beamMesh, origin + originOffset, Quaternion.identity, beamMat, 0);
        }
    }
}

