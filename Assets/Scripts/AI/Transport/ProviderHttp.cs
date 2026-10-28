using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VRD_AIProviders
{
    /// <summary>
    /// Parent class for provider adapters: URL/key resolution, auth, timeout, error formatting
    /// </summary>
    public static class ProviderHttp
    {
        public const int DefaultTimeoutSeconds = 30;

        public readonly struct Target
        {
            public readonly AIModelDefinition Model;
            public readonly ProviderDefinition Provider;
            public readonly LocalKeyStore.ProviderKey Key;
            public readonly string Url;
            public string Label => $"{Provider.providerId}/{Model.modelId}";

            public Target(AIModelDefinition Model, ProviderDefinition provider, LocalKeyStore.ProviderKey key, string url)
            {
                this.Model = Model;
                Provider = provider;
                Key = key;
                Url = url;
            }
        }

        public readonly struct Result
        {
            public readonly bool OK;
            public readonly long Code;
            public readonly string Body;
            public readonly string Error;

            private Result(bool ok, long code, string body, string error)
            {
                OK = ok;
                Code = code;
                Body = body;
                Error = error;
            }

            public static Result Success(long code, string body) => new Result(true, code, body, null);
            public static Result Failure(long code, string body, string error) => new Result(false, code, body, error);
        }

        public static bool TryResolve(AIModelDefinition aiModel, string endpoint, out Target target, out string error)
        {
            target = default;
            if (aiModel == null || aiModel.provider == null)
            {
                error = "Model definition or its provider is not assigned.";
                return false;
            }

            ProviderDefinition provider = aiModel.provider;
            if (string.IsNullOrWhiteSpace(provider.baseUrl))
            {
                error = $"Provider \"{provider.providerId}\" has no base URL.";
                return false;
            }

            if (!LocalKeyStore.TryGet(provider.providerId, out LocalKeyStore.ProviderKey key, out error)) return false;
            string url = provider.baseUrl.Trim().TrimEnd('/') + "/" + endpoint.TrimStart('/');
            target = new Target(aiModel, provider, key, url);
            return true;
        }

        public static async Task<Result> SendAsync(UnityWebRequest req, Target target,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            req.timeout = timeoutSeconds;
            if (req.downloadHandler == null) req.downloadHandler = new DownloadHandlerBuffer();
            if (!TryApplyAuth(req, target, out string authError)) return Result.Failure(0, null, authError);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            
            string body = req.downloadHandler.text;
            if(req.result == UnityWebRequest.Result.Success) return Result.Success(req.responseCode, body);

            if (req.responseCode == 429) LogRateLimitHeaders(req, target);
            string detail = ProviderErrors.Extract(body);
            return Result.Failure(req.responseCode, body,
                $"{target.Label}: HTTP {req.responseCode} {req.error}" +
                (detail != null ? $" - {detail}" : string.Empty));
        }

        public static string Preview(string body) => string.IsNullOrEmpty(body)
            ? "(empty body)"
            : body.Substring(0, Math.Min(200, body.Length));

        private static bool TryApplyAuth(UnityWebRequest req, Target target, out string error)
        {
            error = null;
            try
            {
                req.SetRequestHeader("Authorization", "Bearer " + target.Key.apiKey);
                if (!string.IsNullOrEmpty(target.Key.organization))
                    req.SetRequestHeader("OpenAI-Organization", target.Key.organization);
                return true;
            }
            catch (InvalidOperationException)
            {
                error = $"The \"{target.Provider.providerId}\" key in keys.json contains invalid characters. ";
                return false;
            }
        }

        private static void LogRateLimitHeaders(UnityWebRequest req, Target target)
        {
            var headers = req.GetResponseHeaders();
            if (headers == null) return;
            Debug.LogWarning($"[{target.Label}] 429 headers:\n" + string.Join("\n", headers.Where(h => h.Key.IndexOf("ratelimit", StringComparison.OrdinalIgnoreCase) >= 0 || h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase)).Select(h => $"{h.Key}: {h.Value}")));
        }
    }
}

