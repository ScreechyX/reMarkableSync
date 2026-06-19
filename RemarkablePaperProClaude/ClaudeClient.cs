using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Minimal client for the Anthropic Messages API with vision input.
    ///
    /// This talks to the API over raw HTTPS rather than the official Anthropic C#
    /// SDK on purpose: the tool targets .NET Framework 4.8 so it can share the
    /// RemarkableSync rendering library, and a single, well-scoped HttpClient call
    /// keeps the dependency surface tiny. The wire shape follows the documented
    /// Messages API (image content block + text instruction).
    /// </summary>
    public class ClaudeClient : IDisposable
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        public ClaudeClient(string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Anthropic API key is required", nameof(apiKey));

            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? "claude-opus-4-8" : model;

            // .NET Framework defaults can negotiate down to TLS that the API rejects.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Sends a PNG of a handwritten page to Claude and returns its text answer.
        /// </summary>
        public async Task<string> AnswerHandwrittenPageAsync(
            byte[] pngImage, string systemPrompt, string userInstruction, CancellationToken ct)
        {
            string base64 = Convert.ToBase64String(pngImage);

            var requestBody = new
            {
                model = _model,
                max_tokens = 4096,
                // Adaptive thinking lets the model reason about ambiguous handwriting
                // before answering; the empty thinking block it produces is skipped below.
                thinking = new { type = "adaptive" },
                output_config = new { effort = "high" },
                system = systemPrompt,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "image/png",
                                    data = base64
                                }
                            },
                            new { type = "text", text = userInstruction }
                        }
                    }
                }
            };

            string json = JsonSerializer.Serialize(requestBody);

            using (var req = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                req.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    string respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            $"Claude API request failed ({(int)resp.StatusCode} {resp.StatusCode}): {respBody}");
                    }
                    return ExtractText(respBody);
                }
            }
        }

        /// <summary>
        /// Pulls the concatenated text blocks out of a Messages API response,
        /// skipping thinking blocks and surfacing safety refusals.
        /// </summary>
        private static string ExtractText(string responseJson)
        {
            using (JsonDocument doc = JsonDocument.Parse(responseJson))
            {
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("stop_reason", out JsonElement stop) &&
                    stop.ValueKind == JsonValueKind.String &&
                    stop.GetString() == "refusal")
                {
                    return "[Claude declined to answer this request.]";
                }

                var sb = new StringBuilder();
                if (root.TryGetProperty("content", out JsonElement content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement block in content.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out JsonElement type) &&
                            type.GetString() == "text" &&
                            block.TryGetProperty("text", out JsonElement text))
                        {
                            sb.Append(text.GetString());
                        }
                    }
                }

                string result = sb.ToString().Trim();
                return result.Length == 0 ? "[No answer returned.]" : result;
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
