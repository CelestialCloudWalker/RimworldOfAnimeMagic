using AnimeArsenal;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class Graphic_SingleWithShader : Graphic_Single
    {
        public new Shader Shader
        {
            get
            {
                return AssetBundleShaderManager.GetShaderByAssetName((this.data as GraphicDataWithShader).customShaderName);
            }
        }

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            Material mat = base.MatAt(rot, thing);
            mat.shader = Shader;
            foreach (var item in this.data.shaderParameters)
            {
                item.Apply(mat);
            }
            return mat;
        }
    }
}