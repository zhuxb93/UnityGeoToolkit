using UnityEngine;

namespace GeoToolkit
{
    public static class MaterialUtils
    {
        public static Color RandomRedToGreen()
        {
            return Color.Lerp(Color.red, Color.green, Random.Range(0f, 1f));
        }

        public static Color ToHDR(Color color, float intensity = 1f)
        {
            float multiplier = Mathf.Pow(2, intensity);
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a
            );
        }

        public static void ApplyTreeMaterial(GameObject treeObj, Color color)
        {
            var renderers = treeObj.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material newMat = new Material(materials[i]);
                    newMat.SetColor("_TreeInstanceColor", color);
                    materials[i] = newMat;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }

}
