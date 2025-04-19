using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarInsuranceBot.Services
{
    public class OpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        public OpenAiService()
        {
            _apiKey = Environment.GetEnvironmentVariable("CARINSURANCEBOT_OPENAI_API_KEY");
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        public async Task<string> ChatGPTResponse(string systemPrompt, string userPrompt)
        {
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseString);
            var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            return reply;
        }
    }
}