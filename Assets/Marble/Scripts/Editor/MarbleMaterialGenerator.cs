using UnityEngine;
using UnityEditor;
using System.IO;

namespace Marble.Editor
{
    public static class MarbleMaterialGenerator
    {
        private static readonly string[] CountryCodes =
        {
            "Arg", "Bra", "Can", "China", "Den", "Eng", "Estonia", "Fra",
            "Ger", "Ind", "Ita", "Jap", "Rus", "SA", "Uae", "Usa"
        };

        private static readonly string[] CountryNames =
        {
            "Argentina", "Brazil", "Canada", "China", "Denmark", "England", "Estonia", "France",
            "Germany", "India", "Italy", "Japan", "Russia", "SaudiArabia", "UAE", "USA"
        };

        private const string TexturePath = "Assets/Games/Marble/Textures/TeamFlag";
        private const string MaterialPath = "Assets/Games/Marble/Materials";

        [MenuItem("Marble/Generate Country Materials")]
        public static void GenerateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialPath))
            {
                string parent = Path.GetDirectoryName(MaterialPath).Replace("\\", "/");
                string folderName = Path.GetFileName(MaterialPath);
                AssetDatabase.CreateFolder(parent, folderName);
            }

            int created = 0;
            int updated = 0;

            for (int i = 0; i < CountryCodes.Length; i++)
            {
                string textureName = $"Tex_{CountryCodes[i]}";
                string materialName = $"Mat_{CountryNames[i]}";

                string texturePath = $"{TexturePath}/{textureName}.png";
                string materialFilePath = $"{MaterialPath}/{materialName}.mat";

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                {
                    texturePath = $"{TexturePath}/{textureName}.jpg";
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                }
                if (texture == null)
                {
                    string[] guids = AssetDatabase.FindAssets($"{textureName} t:Texture2D", new[] { TexturePath });
                    if (guids.Length > 0)
                    {
                        texturePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    }
                }

                if (texture == null)
                {
                    Debug.LogWarning($"Could not find texture: {textureName}");
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFilePath);
                bool isNew = material == null;

                if (isNew)
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader == null)
                    {
                        Debug.LogError("URP Lit shader not found. Make sure URP is installed.");
                        return;
                    }
                    material = new Material(urpShader);
                }

                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Metallic", 0.3f);
                material.SetFloat("_Smoothness", 0.7f);

                if (isNew)
                {
                    AssetDatabase.CreateAsset(material, materialFilePath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(material);
                    updated++;
                }

                Debug.Log($"Material {(isNew ? "created" : "updated")}: {materialName}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Material generation complete. Created: {created}, Updated: {updated}");
        }

        [MenuItem("Marble/Assign Materials to SpawnPoints")]
        public static void AssignMaterialsToSpawnPoints()
        {
            var spawnPoints = Object.FindFirstObjectByType<MarbleSpawnPoints>();
            if (spawnPoints == null)
            {
                Debug.LogError("No MarbleSpawnPoints found in scene!");
                return;
            }

            for (int i = 0; i < CountryNames.Length && i < spawnPoints.marbles.Count; i++)
            {
                string materialPath = $"{MaterialPath}/Mat_{CountryNames[i]}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                if (mat != null)
                {
                    var data = spawnPoints.marbles[i];
                    data.material = mat;
                    spawnPoints.marbles[i] = data;
                }
            }

            EditorUtility.SetDirty(spawnPoints);
            Debug.Log("Materials assigned to SpawnPoints.");
        }
    }
}
