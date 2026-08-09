using System.Collections.Generic;
using Gaffer.Common.Localization;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Traits;
using Gaffer.Infrastructure.Configuration;
using Gaffer.Infrastructure.Localization;
using UnityEditor;
using UnityEngine;

namespace Gaffer.Editor.Content
{
    /// <summary>
    /// Materialises the built-in content catalogs as tunable assets, once, on first use — one
    /// <see cref="TraitSO"/> per trait and one <see cref="DramaEventSO"/> per drama event under
    /// Assets/_Project/Settings/Content, wired into a <see cref="TraitCatalogSO"/> and a
    /// <see cref="DramaCatalogSO"/> the editor windows pre-assign. The same rule as BalanceAssets
    /// (decision #28): created through the Unity API so serialisation always matches the SO fields —
    /// no hand-authored YAML — and an existing asset is loaded, never overwritten, so retuning in the
    /// Inspector survives. New content = a new asset added to the catalog list (NON-NEGOTIABLE #3).
    /// </summary>
    public static class ContentAssets
    {
        private const string Root = "Assets/_Project";
        private const string SettingsFolder = "Settings";
        private const string ContentFolder = "Content";
        private const string TraitsFolder = "Traits";
        private const string DramaFolder = "Drama";

        private static string Dir => Root + "/" + SettingsFolder + "/" + ContentFolder;

        private static string TraitsDir => Dir + "/" + TraitsFolder;

        private static string DramaDir => Dir + "/" + DramaFolder;

        /// <summary>The trait catalog asset, created from <see cref="TraitCatalog.Default"/> on first use.</summary>
        public static TraitCatalogSO Traits()
        {
            string path = Dir + "/TraitCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalogSO>(path);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolders();
            var assets = new List<TraitSO>();
            foreach (Trait trait in TraitCatalog.Default.Traits)
            {
                var asset = ScriptableObject.CreateInstance<TraitSO>();
                asset.Author(trait);
                AssetDatabase.CreateAsset(asset, TraitsDir + "/" + trait.Id.Value + ".asset");
                assets.Add(asset);
            }

            catalog = ScriptableObject.CreateInstance<TraitCatalogSO>();
            catalog.Author(assets);
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>The drama catalog asset, created from <see cref="DramaCatalog.Default"/> on first use.</summary>
        public static DramaCatalogSO Drama()
        {
            string path = Dir + "/DramaCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<DramaCatalogSO>(path);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolders();
            var assets = new List<DramaEventSO>();
            foreach (DramaEvent dramaEvent in DramaCatalog.Default.Events)
            {
                var asset = ScriptableObject.CreateInstance<DramaEventSO>();
                asset.Author(dramaEvent);
                AssetDatabase.CreateAsset(asset, DramaDir + "/" + dramaEvent.Id.Value + ".asset");
                assets.Add(asset);
            }

            catalog = ScriptableObject.CreateInstance<DramaCatalogSO>();
            catalog.Author(assets);
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>
        /// The string table asset, created from <see cref="GameStrings.Default"/> on first use — the
        /// player-facing copy, one row per key with English and Turkish side by side.
        ///
        /// <para>Same rule as the catalogs above (decision #28): created through the Unity API so the
        /// serialisation always matches the SO's fields, and an EXISTING asset is loaded and never
        /// overwritten — a line the owner rewrote in the Inspector survives every subsequent run of
        /// this command. New copy that must reach an edited asset is added to the asset, not by
        /// deleting it; <see cref="GameStrings"/> is the floor, not a sync source.</para>
        /// </summary>
        public static StringTableSO Strings()
        {
            string path = Dir + "/StringTable.asset";
            var table = AssetDatabase.LoadAssetAtPath<StringTableSO>(path);
            if (table != null)
            {
                return table;
            }

            EnsureFolders();
            var authored = new List<LocalizedStringRow>();
            IReadOnlyList<StringTableEntry> entries = GameStrings.Default.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                StringTableEntry entry = entries[i];
                var row = new LocalizedStringRow();
                row.Author(entry.Key, entry.Find(Locales.Reference), entry.Find(Locales.Turkish));
                authored.Add(row);
            }

            table = ScriptableObject.CreateInstance<StringTableSO>();
            table.Author(authored);
            AssetDatabase.CreateAsset(table, path);
            AssetDatabase.SaveAssets();
            return table;
        }

        [MenuItem("Gaffer/Content/Create Default Content Assets")]
        public static void CreateAll()
        {
            Traits();
            Drama();
            Strings();
            EditorUtility.DisplayDialog(
                "Gaffer Content", "Trait and drama catalogs and the string table are in " + Dir + ".", "OK");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root + "/" + SettingsFolder, Root, SettingsFolder);
            EnsureFolder(Dir, Root + "/" + SettingsFolder, ContentFolder);
            EnsureFolder(TraitsDir, Dir, TraitsFolder);
            EnsureFolder(DramaDir, Dir, DramaFolder);
        }

        private static void EnsureFolder(string full, string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
