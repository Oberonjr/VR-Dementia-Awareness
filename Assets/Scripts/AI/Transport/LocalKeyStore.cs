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

        public class ProviderKey
        {
            public string apiKey;
            public string organization;
        }

        /// <summary>
        ///  Absolute path where keys.json is expected on the current platform
        /// </summary>
        public static string KeyFilePath
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".vrdementia", FileName);
#else
                return Path.Combine(Application.persistentDataPath, FileName);                
#endif
            }
        }
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
                error = $"No key file found at:\n{path}\nCreate it with an entry for \"{providerID}\".";
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

            error = null;
            return true;
        }
    }

}
