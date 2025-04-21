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
            // Retrieve API key from environment variables.
            // If the variable is not set, throw a clear exception.
            _apiKey = Environment.GetEnvironmentVariable("CARINSURANCEBOT_OPENAI_API_KEY") 
                ?? throw new InvalidOperationException("OpenAI API key is missing from environment variables.");

            if (string.IsNullOrEmpty(_apiKey))
                throw new Exception("OpenAI API key is missing from environment variables.");

            // Initialize HttpClient and set base address for OpenAI API
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/v1/")
            };

            // Add the API key to authorization headers for all outgoing requests
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        /// <summary>
        /// Sends a message to OpenAI using GPT-3.5-turbo model and retrieves the response.
        /// </summary>
        /// <param name="systemPrompt">The system instruction (guides behavior of the assistant).</param>
        /// <param name="userPrompt">The user message (the actual input from user).</param>
        /// <returns>The assistant's reply as a string.</returns>
        public async Task<string> ChatGPTResponse(string systemPrompt, string userPrompt)
        {
            // Build the request payload with the model and messages
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt }, // Instructions for the assistant
                    new { role = "user", content = userPrompt } // The user's message
                }
            };

            // Serialize the request body to JSON
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Send POST request to the completions endpoint
            var response = await _httpClient.PostAsync("chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            // If the API call failed, throw an error with detailed info
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI request failed: {response.StatusCode}\n{responseString}");
            }

            // Parse the response JSON document
            using var doc = JsonDocument.Parse(responseString);
            var choices = doc.RootElement.GetProperty("choices");
            
            // Ensure at least one choice is returned
            if (choices.GetArrayLength() == 0)
                throw new Exception("No response from OpenAI.");

            // Extract the content from the first response choice
            var reply = choices[0].GetProperty("message").GetProperty("content").GetString()
                ?? throw new Exception("OpenAI returned an empty message.");

            // Optional: log the full raw response (for debugging)
            //Console.WriteLine($"OpenAI raw response:{responseString}");

            return reply;
        }
    }
}