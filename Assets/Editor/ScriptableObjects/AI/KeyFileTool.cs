using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace VRD_AIProviders
{
    public static class KeyFileTool
    {
        public enum State {NoProviderId, NoFile, InvalidJson, NoEntry, EmptyKey, Placeholder, OK}
        
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static JProperty FindEntry(JObject root, string providerId) =>
            root.Properties().FirstOrDefault(x =>
                string.Equals(x.Name, providerId?.Trim(), StringComparison.OrdinalIgnoreCase));
        //Index of the root '{', skipping whitespace, BOM and comments. -1 if not found.
        private static int FindRootBrace(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c  = text[i];
                if (char.IsWhiteSpace(c) || c == '\uFEFF')
                {
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    int nl = text.IndexOf('\n', i);
                    i = nl < 0 ? text.Length : nl + 1;
                    continue;
                }

                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 2;
                    continue;
                }
                return c == '{' ? i : -1;
            }

            return -1;
        }
        public static List<ProviderDefinition> AllProviders() =>
            AssetDatabase.FindAssets("t:ProviderDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<ProviderDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.providerId))
                .GroupBy(p => p.providerId.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .OrderBy(p => p.providerId, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private static string BuildEntry(ProviderDefinition p, bool trailingComma)
        {
            const string indent = "  ";
            string label = p.name.StartsWith("Provider_") ? p.name.Substring("Provider_".Length) : p.name;

            var sb = new StringBuilder();
            sb.Append(indent).Append("// ").Append(label);
            if (!string.IsNullOrWhiteSpace(p.keyPageUrl))
            {
                sb.Append(" - get a key at ").Append(p.keyPageUrl.Trim());
            }

            sb.AppendLine();
            if(!string.IsNullOrWhiteSpace(p.keySetupHint)) sb.Append(indent).Append("// ").AppendLine(p.keySetupHint.Trim());
            sb.Append(indent).Append(JsonConvert.ToString(p.providerId.Trim().ToLowerInvariant()))
                .Append(": { \"apiKey\": \"").Append(LocalKeyStore.PlaceholderKey).Append("\" }");
            if (trailingComma) sb.Append(',');
            sb.AppendLine();
            return sb.ToString();
        }
        public static string BuildTemplate(IEnumerable<ProviderDefinition> providers)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ===== VR Dementia Awareness - API keys =====");
            sb.AppendLine("// This file holds SECRET keys. It stays on THIS computer only:");
            sb.AppendLine("// never move it into the Unity project folder, never commit it, never paste it in chat.");
            sb.AppendLine("//");
            sb.AppendLine("// HOW TO FILL IT IN");
            sb.AppendLine("//  1. Find the provider you need below.");
            sb.AppendLine($"//  2. Replace {LocalKeyStore.PlaceholderKey} with your key. Keep the \"quotes\" around it.");
            sb.AppendLine("//  3. Save the file (Ctrl+S). Unity reads it the next time it needs a key - no restart needed.");
            sb.AppendLine("//  4. In Unity, click the provider asset: the API Key box turns from yellow to blue when it works.");
            sb.AppendLine("//");
            sb.AppendLine("// RULES");
            sb.AppendLine("//  - Lines starting with // are notes and are ignored.");
            sb.AppendLine("//  - The name before the colon must match \"Provider Id\" on the provider asset in Unity.");
            sb.AppendLine("//  - Entries are separated by commas. The last entry has no comma after it.");
            sb.AppendLine("//  - Providers you don't use can keep the placeholder.");
            sb.AppendLine("//  - OpenAI only (optional): add  , \"organization\": \"org-...\"  after the key, inside the { }.");
            sb.AppendLine("//");
            sb.AppendLine("// EXAMPLE OF A FILLED-IN ENTRY");
            sb.AppendLine("//   \"groq\": { \"apiKey\": \"gsk_abc123...\" },");
            sb.AppendLine("{");
            List<ProviderDefinition> list = providers.ToList();
            for (int i = 0; i < list.Count; i++)
                sb.Append(BuildEntry(list[i], i < list.Count - 1));
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static State GetState(string providerId, out string message)
        {
            if (LocalKeyStore.TryGet(providerId, out _, out message))
            {
                message = "Key found. Ready to use.";
                return State.OK;
            }
            if (string.IsNullOrWhiteSpace(providerId)) return State.NoProviderId;

            string path = LocalKeyStore.KeyFilePath;
            if (!File.Exists(path)) return State.NoFile;

            JObject root;
            try { root = JObject.Parse(File.ReadAllText(path)); }
            catch { return State.InvalidJson; }

            JProperty entry = FindEntry(root, providerId);
            if (entry == null) return State.NoEntry;
            
            string k = (string)entry.Value?["apiKey"];
            if (string.IsNullOrWhiteSpace(k)) return State.EmptyKey;
            if (k.Trim() == LocalKeyStore.PlaceholderKey) return State.Placeholder;
            return State.OK;
        }

        public static string EnsureFile()
        {
            string path = LocalKeyStore.KeyFilePath;
            Directory.CreateDirectory(LocalKeyStore.KeyFolder);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, BuildTemplate(AllProviders()), Utf8NoBom);
                Debug.Log($"[Keys] Created template key file at {path}");
            }

            return path;
        }

        public static void OpenFile()
        {
            string path = EnsureFile();
#if UNITY_EDITOR_WIN
            try
            {
                System.Diagnostics.Process.Start("notepad.exe", $"\"{path}\"");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Tried to open notepad.exe but failed because: " + e.Message + ". Attempting to open with the machine's default application.");
                EditorUtility.OpenWithDefaultApp(path);
            }
            
#else
            EditorUtility.OpenWithDefaultApp(path);
#endif
        }

        public static void RevealFolder()
        {
            Directory.CreateDirectory(LocalKeyStore.KeyFolder);
            string path = LocalKeyStore.KeyFilePath;
            EditorUtility.RevealInFinder(File.Exists(path) ? path : LocalKeyStore.KeyFolder);
        }

        public static bool TryAddEntry(ProviderDefinition p, out string error)
        {
            error = null;
            string path = EnsureFile();
            string text = File.ReadAllText(path);

            JObject root;
            try { root = JObject.Parse(text); }
            catch (Exception e)
            {
                error = $"keys.json has a formatting mistake. Fix it (or delete the file to get a fresh template) first.\n\n{e.Message}";
                return false;
            }
            if (FindEntry(root, p.providerId) != null) return true;

            int brace = FindRootBrace(text);
            if (brace < 0) { error = "Could not find the opening { in keys.json."; return false; }

            string entry = Environment.NewLine + BuildEntry(p, root.Count > 0).TrimEnd('\r', '\n');
            File.WriteAllText(path, text.Insert(brace + 1, entry), Utf8NoBom);
            return true; 
        }

        public static int AddAllMissing(out string error)
        {
            error = null;
            int added = 0;
            foreach (ProviderDefinition p in AllProviders())
            {
                if (GetState(p.providerId, out _) != State.NoEntry) continue;
                if (!TryAddEntry(p, out error)) return added;
                added++;
            }
            return added;
        }

        public static void ChooseFolder()
        {
            string start = Directory.Exists(LocalKeyStore.KeyFolder)
                ? LocalKeyStore.KeyFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string picked =
                EditorUtility.OpenFolderPanel("Choose the folder that holds (or should hold) keys.json", start, "");
            if(string.IsNullOrEmpty(picked)) return;
            picked = Path.GetFullPath(picked);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (picked.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase) &&
                !EditorUtility.DisplayDialog("Folder is inside the Unity project",
                    "keys.json in this folder could get committed to git and leak your keys.\n\nPick a folder outside the project unless you're sure.",
                    "Use anyway [ONLY IF YOU KNOW EXACTLY WHAT YOU ARE DOING]", "Cancel"))
            {
                return;
            }

            SwitchFolder(string.Equals(picked.TrimEnd('\\', '/'), LocalKeyStore.DefaultKeyFolder.TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase)
                ? ""
                : picked);
            
        }
        
        public static void SwitchFolder(string newOverride)
        {
            string oldFolder = LocalKeyStore.KeyFolder;
            string oldPath = LocalKeyStore.KeyFilePath;
            LocalKeyStore.FolderOverride = newOverride;
            string newPath = LocalKeyStore.KeyFilePath;

            if (string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase)) return;
            if (!File.Exists(oldPath) || File.Exists(newPath)) return;

            // 0 = ok (Enter), 1 = cancel (Esc / X), 2 = alt
            int choice = EditorUtility.DisplayDialogComplex("Bring your keys along?",
                $"There's no keys.json in the new folder yet.\n\nCurrent file:\n{oldPath}",
                "Copy (keep files in old location)",
                "Leave new folder empty",
                "Move (remove files from old location)");
            if (choice == 1) return;

            try
            {
                Directory.CreateDirectory(LocalKeyStore.KeyFolder);
                File.Copy(oldPath, newPath);
                if (choice == 2)
                {
                    File.Delete(oldPath);
                    DeleteFolderIfEmpty(oldFolder);
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Couldn't transfer keys.json", e.Message, "OK");
            }
        }

        private static void DeleteFolderIfEmpty(string folder)
        {
            //Only able to delete the default folder - any other created folder cannot be removed
            if (!string.Equals(folder.TrimEnd('\\', '/'), LocalKeyStore.DefaultKeyFolder.TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase)) return;
            if(Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        
        
        
        [MenuItem("Tools/VR Dementia/API Keys/Open keys file")]
        private static void MenuOpen() => OpenFile();

        [MenuItem("Tools/VR Dementia/API Keys/Show keys folder")]
        private static void MenuReveal() => RevealFolder();

        [MenuItem("Tools/VR Dementia/API Keys/Add missing providers to keys file")]
        private static void MenuAddAll()
        {
            int n = AddAllMissing(out string error);
            if (error != null) EditorUtility.DisplayDialog("Couldn't update keys.json", error, "OK");
            else Debug.Log($"[Keys] Added {n} missing provider entr{(n == 1 ? "y" : "ies")} to {LocalKeyStore.KeyFilePath}");
        }

        [MenuItem("Tools/VR Dementia/API Keys/Use a different folder...")]
        private static void MenuChoose() => ChooseFolder();

        [MenuItem("Tools/VR Dementia/API Keys/Reset to default folder")]
        private static void MenuReset() => SwitchFolder("");
    }
}

