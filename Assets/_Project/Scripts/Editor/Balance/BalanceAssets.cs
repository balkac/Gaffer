using Gaffer.Infrastructure.Configuration;
using UnityEditor;
using UnityEngine;

namespace Gaffer.Editor.Balance
{
    /// <summary>
    /// Loads the default balance assets, creating each once on first use, so the editor windows can
    /// pre-assign them without the developer hand-creating an SO. The assets live under
    /// Assets/_Project/Settings/Balance and start at the calibrated defaults (edit them to retune). Unity
    /// serialises them, so the values always match the SO's fields — no hand-authored YAML.
    /// </summary>
    public static class BalanceAssets
    {
        private const string Root = "Assets/_Project";
        private const string SettingsFolder = "Settings";
        private const string BalanceFolder = "Balance";

        private static string Dir => Root + "/" + SettingsFolder + "/" + BalanceFolder;

        public static SimulationBalanceSO Simulation()
        {
            return LoadOrCreate<SimulationBalanceSO>("SimulationBalance");
        }

        public static DevelopmentBalanceSO Development()
        {
            return LoadOrCreate<DevelopmentBalanceSO>("DevelopmentBalance");
        }

        public static RenewalBalanceSO Renewal()
        {
            return LoadOrCreate<RenewalBalanceSO>("RenewalBalance");
        }

        public static DramaBalanceSO Drama()
        {
            return LoadOrCreate<DramaBalanceSO>("DramaBalance");
        }

        [MenuItem("Gaffer/Balance/Create Default Balance Assets")]
        public static void CreateAll()
        {
            Simulation();
            Development();
            Renewal();
            Drama();
            EditorUtility.DisplayDialog("Gaffer Balance", "Default balance assets are in " + Dir + ".", "OK");
        }

        private static T LoadOrCreate<T>(string assetName) where T : ScriptableObject
        {
            string path = Dir + "/" + assetName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolders();
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureFolders()
        {
            string settings = Root + "/" + SettingsFolder;
            if (!AssetDatabase.IsValidFolder(settings))
            {
                AssetDatabase.CreateFolder(Root, SettingsFolder);
            }

            if (!AssetDatabase.IsValidFolder(Dir))
            {
                AssetDatabase.CreateFolder(settings, BalanceFolder);
            }
        }
    }
}
