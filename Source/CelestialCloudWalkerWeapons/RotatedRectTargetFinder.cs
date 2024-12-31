using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public static class RotatedRectTargetFinder
    {
        public static List<Thing> GetTargetsInRotatedRect(Map map, Vector3 origin, float width, float height, float angle)
        {
            List<IntVec3> cells = GetCellsInRotatedRect(map, origin, width, height, angle);
            List<Thing> targets = new List<Thing>();

            foreach (IntVec3 cell in cells)
            {
                if (cell.InBounds(map))
                {
                    targets.AddRange(cell.GetThingList(map));
                }
            }

            return targets;
        }

        public static List<IntVec3> GetCellsInRotatedRect(Map map, Vector3 origin, float width, float height, float angle)
        {
            Vector3[] corners = GetRotatedRectCorners(origin, width, height, angle);
            return GetCellsInRotatedRect(corners);
        }

        private static Vector3[] GetRotatedRectCorners(Vector3 origin, float width, float height, float angle)
        {
            float halfWidth = width / 2f;
            float halfHeight = height;

            Vector3[] corners = new Vector3[4];
            //topright
            corners[0] = new Vector3(-halfWidth, 0f, 0);
            //topleft
            corners[1] = new Vector3(halfWidth, 0f, 0);
            //bottom left
            corners[2] = new Vector3(halfWidth, 0f, height);
            //bottom right
            corners[3] = new Vector3(-halfWidth, 0f, height);

            for (int i = 0; i < 4; i++)
            {
                corners[i] = RotatePointAroundPivot(corners[i], Vector3.zero, Quaternion.Euler(0, angle, 0));
                corners[i] += origin;
            }

            return corners;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        private static List<IntVec3> GetCellsInRotatedRect(Vector3[] corners)
        {
            List<IntVec3> cells = new List<IntVec3>();

            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            foreach (Vector3 corner in corners)
            {
                minX = Mathf.Min(minX, corner.x);
                minZ = Mathf.Min(minZ, corner.z);
                maxX = Mathf.Max(maxX, corner.x);
                maxZ = Mathf.Max(maxZ, corner.z);
            }

            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
            {
                for (int z = Mathf.FloorToInt(minZ); z <= Mathf.CeilToInt(maxZ); z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (IsPointInPolygon(cell.ToVector3(), corners))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private static bool IsPointInPolygon(Vector3 point, Vector3[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (((polygon[i].z <= point.z && point.z < polygon[j].z) || (polygon[j].z <= point.z && point.z < polygon[i].z)) &&
                    (point.x < (polygon[j].x - polygon[i].x) * (point.z - polygon[i].z) / (polygon[j].z - polygon[i].z) + polygon[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static List<Thing> GetTargetsInCone(Map map, Vector3 origin, float radius, float angle, float direction, float offsetX = 0f, float offsetZ = 0f)
        {
            List<IntVec3> cells = GetCellsInCone(map, origin, radius, angle, direction, offsetX, offsetZ);
            return cells.SelectMany(c => c.GetThingList(map)).ToList();
        }

        public static List<IntVec3> GetCellsInCone(Map map, Vector3 origin, float radius, float angle, float direction, float offsetX = 0f, float offsetZ = 0f)
        {
            Vector3 offset = Quaternion.Euler(0, direction, 0) * new Vector3(offsetX, 0f, offsetZ);
            Vector3 offsetOrigin = origin + offset;

            IntVec3 originCell = offsetOrigin.ToIntVec3();
            float halfAngle = angle / 2f;

            return GenRadial.RadialCellsAround(originCell, radius, true)
                .Where(c =>
                {
                    if (c == originCell) return true;
                    float cellAngle = (c.ToVector3() - offsetOrigin).AngleFlat();
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(cellAngle, direction));
                    return angleDiff <= halfAngle;
                })
                .ToList();
        }
    }
}