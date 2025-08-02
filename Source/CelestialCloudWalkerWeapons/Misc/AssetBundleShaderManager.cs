using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{

    [StaticConstructorOnStartup]
    public static class AssetBundleShaderManager
    {
        private static Dictionary<string, Shader> ShaderCache = new Dictionary<string, Shader>();

        public static bool Cached = false;



        static AssetBundleShaderManager()
        {
            if (!AssetBundleShaderManager.Cached)
            {
                AssetBundleShaderManager.CacheAllLoadedShaders();
            }
        }


        public static bool HasShader(string ShaderName)
        {
            return ShaderCache.ContainsKey(ShaderName);
        }




        public static bool TryGet(string ShaderName, out Shader shader)
        {
            shader = null;
            if (ShaderCache.ContainsKey(ShaderName))
            {
                shader = GetShaderByAssetName(ShaderName);
                return true;
            }
            else
            {
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    foreach (var bundle in mod.assetBundles.loadedAssetBundles)
                    {
                        foreach (var item in bundle.LoadAllAssets())
                        {
                            if (item is Shader foundshader)
                            {
                                RegisterShader(item.name, foundshader);
                                shader = foundshader;
                                return true;
                            }
                        }
                    }
                }

            }
            return false;
        }




        public static void RegisterShader(string ShaderName, Shader Shader)
        {
            if (!HasShader(ShaderName))
            {
                Log.Message($"Shader Cache registering Shader {ShaderName}");
                ShaderCache.Add(ShaderName, Shader);
            }
            else
            {
                Log.Message($"Shader Cache updating Shader {ShaderName}");
                ShaderCache[ShaderName] = Shader;
            }
        }

        public static Shader GetShaderByAssetName(string ShaderName)
        {
            if (HasShader(ShaderName))
            {
                return ShaderCache[ShaderName];
            }
            Log.Message($"{ShaderName} not found.");
            return null;
        }


        public static void CacheAllLoadedShaders()
        {
            Log.Message("Beginning Shader Cache");

            foreach (var mod in LoadedModManager.RunningMods)
            {
                foreach (var bundle in mod.assetBundles.loadedAssetBundles)
                {
                    foreach (var item in bundle.LoadAllAssets())
                    {
                        if (item is Shader shader)
                        {
                            RegisterShader(item.name, shader);

                        }
                    }
                }
            }

            Cached = true;
            Log.Message("Shader Cache complete");
        }

        public static void RemoveShader(string ShaderName)
        {
            if (HasShader(ShaderName))
            {
                ShaderCache.Remove(ShaderName);
            }
        }
    }
}