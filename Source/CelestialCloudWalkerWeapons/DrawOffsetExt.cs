using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class DrawOffsetExt : DefModExtension
    {
        public Vector3 offset;

        // Optional: separate offsets for each direction if needed
        public Vector3 offsetNorth;
        public Vector3 offsetSouth;
        public Vector3 offsetEast;
        public Vector3 offsetWest;

        public Vector3 GetOffsetForRot(Rot4 rot4)
        {
            Vector3 finalOffset = Vector3.zero;

            // Use specific directional offsets if defined
            if (rot4 == Rot4.North && offsetNorth != Vector3.zero)
            {
                finalOffset = offsetNorth;
            }
            else if (rot4 == Rot4.South && offsetSouth != Vector3.zero)
            {
                finalOffset = offsetSouth;
            }
            else if (rot4 == Rot4.East && offsetEast != Vector3.zero)
            {
                finalOffset = offsetEast;
            }
            else if (rot4 == Rot4.West && offsetWest != Vector3.zero)
            {
                finalOffset = offsetWest;
            }
            else
            {
                // Fallback to general offset with rotation logic
                if (rot4 == Rot4.West)
                {
                    // For west-facing, flip both X and Z
                    finalOffset = new Vector3(-offset.x, offset.y, -offset.z);
                }
                else if (rot4 == Rot4.North)
                {
                    // North might need Y adjustment
                    finalOffset = new Vector3(offset.x, offset.y + 0.1f, offset.z);
                }
                else if (rot4 == Rot4.South)
                {
                    // South might need Y adjustment in opposite direction
                    finalOffset = new Vector3(offset.x, offset.y - 0.1f, offset.z);
                }
                else
                {
                    // East - use default offset
                    finalOffset = offset;
                }
            }

            return finalOffset;
        }
    }
}