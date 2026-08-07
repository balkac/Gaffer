using UnityEditor;
using UnityEngine;

namespace Gaffer.Editor.Balance
{
    /// <summary>
    /// Rewrites the balance and content assets from their current class shape.
    /// <para>
    /// Why this exists: Unity only writes the fields a <c>ScriptableObject</c> had when the asset was last
    /// serialized. Add a field to a <c>*BalanceSO</c> and the existing <c>.asset</c> keeps its old, shorter
    /// field list — the new field is absent from the file and runs from the C# initializer instead. Behaviour
    /// stays correct as long as the initializer matches the shipped value (which
    /// <c>SettingsDefaultContractTests</c> pins), but the asset stops being reviewable as balance data, and
    /// the first person to touch it in the Inspector silently bakes in every missing field at once — landing
    /// as a large diff that reads like an intentional balance change. UNITY.md §8 calls this the stale-asset
    /// mirror hazard.
    /// </para>
    /// <para>
    /// Run it after adding or removing a serialized field, then read the diff before committing. Safe to run
    /// at any time: <c>ForceReserializeAssets</c> rewrites the file from the loaded object, so values already
    /// in the asset are preserved and only absent fields are added at their initializer value.
    /// </para>
    /// </summary>
    public static class SettingsAssetMaintenance
    {
        private static readonly string[] SettingsFolders =
        {
            "Assets/_Project/Settings",
        };

        /// <summary>
        /// Re-serializes every asset under the settings folders. Also callable head-less for CI or a scripted
        /// run: <c>Unity -batchmode -quit -projectPath . -executeMethod
        /// Gaffer.Editor.Balance.SettingsAssetMaintenance.ReserializeAll</c>.
        /// </summary>
        [MenuItem("Gaffer/Balance/Reserialize Settings Assets")]
        public static void ReserializeAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", SettingsFolders);
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            if (paths.Length == 0)
            {
                Debug.LogWarning("[Gaffer] No ScriptableObject assets found under " + string.Join(", ", SettingsFolders) + ".");
                return;
            }

            AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gaffer] Re-serialized " + paths.Length + " settings assets. Read the diff before committing.");
        }
    }
}
