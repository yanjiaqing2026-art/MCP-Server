using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using E3DPlugin.Models;
using Newtonsoft.Json;

namespace E3DPlugin.Services
{
    public class E3DMiddlewareClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public string BaseUrl => _baseUrl;

        public E3DMiddlewareClient(string baseUrl = null)
        {
            _baseUrl = baseUrl ?? "http://localhost:7861";
            _http = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
        }

        /// <summary>
        /// 健康检查 — GET /api/e3d/health
        /// </summary>
        public async Task<(bool Available, string Message)> HealthCheckAsync()
        {
            try
            {
                var response = await _http.GetAsync("/api/e3d/health");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return (true, json);
                }
                return (false, $"HTTP {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 验证管道 — POST /api/e3d/validate
        /// </summary>
        public async Task<ValidationResponse> ValidatePipelinesAsync(
            ValidationRequest request)
        {
            var json = JsonConvert.SerializeObject(request, Formatting.Indented);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync("/api/e3d/validate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ValidationResponse>(responseJson);
        }

        /// <summary>
        /// 验证单条管道（便捷方法）
        /// </summary>
        public async Task<ValidationResponse> ValidateSinglePipelineAsync(
            E3DPipelineAttribute pipeline,
            string standard = "ASME B31.3")
        {
            var request = new ValidationRequest
            {
                Pipelines = { pipeline },
                Standard = standard
            };
            return await ValidatePipelinesAsync(request);
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
