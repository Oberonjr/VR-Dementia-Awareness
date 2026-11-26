using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;


namespace VRD_AIProviders
{
    /// <summary>
    /// Reads API keys from a single local keys.json, keyed by <see cref="ProviderDefinition.providerId"/>.
    /// PC/Editor: %USERPROFILE%/.vrdementia/keys.json — Quest: Application.persistentDataPath/keys.json.
    /// </summary>
    public static class LocalKeyStore
    {
        public const string FileName = "keys.json";
        public const string DefaultFolderName = ".vrdementia";
        public const string PlaceholderKey = "PASTE-YOUR-KEY-HERE";
        public class ProviderKey
        {
            public string apiKey;
            public string organization;
        }

        public static string DefaultKeyFolder
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultFolderName);
#else
                return Application.persistentDataPath;
#endif
            }
        }
#if UNITY_EDITOR
        private static string folderOverride;
        private static bool overrideLoaded;
        private static string OverridePrefKey => "VRD.KeyFolderOverride." + UnityEditor.PlayerSettings.productGUID;

        public static string FolderOverride
        {
            get
            {
                if (!overrideLoaded)
                {
                    folderOverride = UnityEditor.EditorPrefs.GetString(OverridePrefKey, "");
                    overrideLoaded = true;
                }

                return folderOverride;
            }
            set
            {
                folderOverride = value?.Trim() ?? "";
                overrideLoaded = true;
                if (string.IsNullOrEmpty(folderOverride))
                {
                    UnityEditor.EditorPrefs.DeleteKey(OverridePrefKey);
                }
                else
                {
                    UnityEditor.EditorPrefs.SetString(OverridePrefKey, folderOverride);
                }
            }
        }
        public static bool IsUsingOverride => !string.IsNullOrEmpty(FolderOverride);
#else
        public static bool IsUsingOverride => false;
#endif
        public static string KeyFolder
        {
            get
            {
#if UNITY_EDITOR
                if (IsUsingOverride) return FolderOverride;
#endif
                return DefaultKeyFolder;
            }
        }
        /// <summary>
        /// Absolute path where keys.json is expected on the current platform
        /// </summary>
        public static string KeyFilePath => Path.Combine(KeyFolder, FileName);
        /// <summary>
        /// Looks up the key for <paramref name="providerID"/>. Returns false with a readable
        /// <paramref name="error"/> describing what is missing or wrong
        /// </summary>
        public static bool TryGet(string providerID, out ProviderKey key, out string error)
        {
            key = null;
            string path = KeyFilePath;
            if (string.IsNullOrWhiteSpace(providerID))
            {
                error = "ProviderID is empty. Set it to the name used for this provider in keys.json.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"No key file found at:\n{path}\nClick \"Create keys file\" on any Provider asset to make one.";
                return false;
            }

            Dictionary<string, ProviderKey> all;
            try
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, ProviderKey>>(File.ReadAllText(path));
                all = parsed == null
                    ? new Dictionary<string, ProviderKey>()
                    : new Dictionary<string, ProviderKey>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                error = $"keys.json is not a valid JSON ({path}):\n{e.Message}";
                return false;
            }

            if (!all.TryGetValue(providerID.Trim(), out key) || key == null)
            {
                string found = all.Count == 0 ? "none" : string.Join(", ", all.Keys.Select(k => $"\"{k}\""));
                error = $"keys.json has no entry for \"{providerID}\". Entries found: {found}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(key.apiKey))
            {
                error = $"The \"{providerID}\" entry in keys.json has an empty \"apiKey\".";
                key = null;
                return false;
            }

            if (key.apiKey.Trim() == PlaceholderKey)
            {
                error =
                    $"The \"{providerID}\" entry in keys.json still has the placeholder. Replace {PlaceholderKey} with your real key.";
                key = null;
                return false;
            }
            error = null;
            return true;
        }
    }

}
