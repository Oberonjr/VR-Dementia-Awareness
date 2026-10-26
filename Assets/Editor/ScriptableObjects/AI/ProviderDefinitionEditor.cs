using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;


namespace VRD_AIProviders
{
    [CustomEditor(typeof(ProviderDefinition))]
    public class ProviderDefinitionEditor : Editor
    {
        private const int TimeOutSeconds = 15;

        private UnityWebRequest pending;
        private string pendingURL;
        private Vector2 scroll;

        private void OnDisable()
        {
            EditorApplication.update -= Poll;
            pending?.Abort();
            pending?.Dispose();
            pending = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            
            SerializedProperty models = serializedObject.FindProperty("cachedModels");
            SerializedProperty status = serializedObject.FindProperty("lastFetchedStatus");
            SerializedProperty ok = serializedObject.FindProperty("lastFetchOK");
            
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Available Models", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(pending != null))
            {
                if (GUILayout.Button(pending != null ? "Fetching..." : "Fetch available models"))
                {
                    Fetch();
                }
            }

            if (!string.IsNullOrEmpty(status.stringValue))
            {
                string msg = status.stringValue;
                if(!ok.boolValue && !string.IsNullOrEmpty(models.stringValue))
                    msg += "\n\nShowing the list from the last successful fetch.";
                EditorGUILayout.HelpBox(msg, ok.boolValue ? MessageType.Info : MessageType.Warning);
            }

            if (string.IsNullOrEmpty(models.stringValue))
            {
                EditorGUILayout.HelpBox("No models found.", MessageType.None);
                return;
            }
            
            //Readonly but selectable for copy-paste
            var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperLeft, wordWrap = false };
            var content = new GUIContent(models.stringValue);
            float height = style.CalcHeight(content, EditorGUIUtility.currentViewWidth);
            scroll = EditorGUILayout.BeginScrollView(scroll, EditorStyles.helpBox, GUILayout.Height(Mathf.Min(height + 8, 220)));
            EditorGUILayout.SelectableLabel(models.stringValue, style, GUILayout.Height(height));
            EditorGUILayout.EndScrollView();
        }

        private void Fetch()
        {
            var def = (ProviderDefinition)target;
            string baseUrl = def.baseUrl?.Trim();

            if (string.IsNullOrEmpty(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Store(def, false, "Base URL must be a full http/-s URL", null);
                return;
            }

            if (!LocalKeyStore.TryGet(def.providerId, out LocalKeyStore.ProviderKey key, out string keyError))
            {
                Store(def, false, keyError, null);
                return;
            }

            pendingURL = baseUrl.TrimEnd('/') + "/models";
            var req = UnityWebRequest.Get(pendingURL);
            try
            {
                req.SetRequestHeader("Authorization", "Bearer " + key.apiKey);
                if (!string.IsNullOrEmpty(key.organization))
                {
                    req.SetRequestHeader("OpenAI-Organization", key.organization);
                }
                
            }
            catch(InvalidOperationException)
            {
                    req.Dispose();
                    Store(def, false, $"The \"{def.providerId}\" key or organization in keys.json contains invalid characters.",null);
                    return;
            }

            req.timeout = TimeOutSeconds;
            req.SendWebRequest();
            pending = req;
            EditorApplication.update += Poll;
        }

        private void Poll()
        {
            if (pending == null || !pending.isDone) return;
            EditorApplication.update -= Poll;
            UnityWebRequest req = pending;
            pending = null;

            try
            {
                HandleResponse((ProviderDefinition)target, req);
            }
            finally
            {
                req.Dispose();
                Repaint();
            }
        }

        private void HandleResponse(ProviderDefinition def, UnityWebRequest req)
        {
            string host = new Uri(pendingURL).Host;

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                Store(def, false, $"Could not reach {host} ({req.error}).\n" + "Check your internet connection, VPN/Firewall, and that the Base URL host is spelled correctly", null);
                return;
            }

            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                long code = req.responseCode;
                string detail = ProviderErrors.Extract(req.downloadHandler?.text);
                string msg = code switch
                {
                    400 => "Bad request (400). The provider rejected the request format; this URL may not be OpenAI-compatible.",
                    401 => $"Key rejected (401). The \"{def.providerId}\" key in keys.json is wrong, revoked, or belongs to a different provider.",
                    403 => "Forbidden (403). The key is valid but not allowed to list models. Check account activation, billing, or project permissions.",
                    404 => $"Not found (404) at {pendingURL}.\nBase Url is probably wrong. Most providers need the version segment, e.g. .../v1 (Groq: .../openai/v1).",
                    429 => "Rate limited or out of quota (429). Wait a moment and retry, or check the account's usage limits.",
                    _ when code >= 500 => $"Provider error ({code}). {host} is having problems; try again later.",
                    _ => $"Unexpected HTTP {code} from {host}."
                };
                if (!string.IsNullOrEmpty(detail)) msg += $"\nProvider says: {detail}";
                Store(def, false, msg, null);
                return;
            }
            
            string body = req.downloadHandler?.text;
            string[] ids;
            try
            {
                var data = JObject.Parse(body)["data"] as JArray;
                if (data == null) throw new FormatException("missing \"data\" array");
                ids = data.Select(m => (string)m["id"]).Where(id => !string.IsNullOrWhiteSpace(id))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception e)
            {
                string preview = string.IsNullOrEmpty(body) ? "(empty body)" : body.Substring(0, Math.Min(200, body.Length));
                Store(def, false,
                    $"Got a response, but not in the expected /models format ({e.Message}).\n" +
                    $"This URL may not be Open-AI compatible.\nResponse starts with: {preview}", null);
                return;
            }

            if (ids.Length == 0)
            {
                Store(def, false, "Request succeeded, but this key has access to 0 models.", null);
                return;
            }

            Store(def, true, $"{ids.Length} models - fetched {DateTime.Now:yyyy-MM-dd HH:mm}", string.Join("\n", ids));
        }

        private static void Store(ProviderDefinition def, bool success, string status, string models)
        {
            var so = new SerializedObject(def);
            so.FindProperty("lastFetchOK").boolValue = success;
            so.FindProperty("lastFetchedStatus").stringValue = status;
            if(models != null) so.FindProperty("cachedModels").stringValue = models;
            so.ApplyModifiedProperties();
        }
    }

}
