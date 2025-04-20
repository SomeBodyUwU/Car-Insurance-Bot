#  Car Insurance Telegram Bot
A simple project with a mock Mindee API request(for now) and usage of Open AI API to support real-like answers from the bot side.
## Setting-up
-In order to have a bot working properly, consider adding environmental variables to your local machine named "CARINSURANCEBOT_TELEGRAM_API_KEY" and "CARINSURANCEBOT_OPENAI_API_KEY". You can use Open AI help page to do that: https://help.openai.com/en/articles/5112595-best-practices-for-api-key-safety
## Required nugget packages
- Telegram.Bot: dotnet add package Telegram.Bot --version 22.5.1 (https://www.nuget.org/packages/Telegram.Bot)
- Open AI: dotnet add package OpenAI --version 2.1.0 (https://www.nuget.org/packages/OpenAI)
- Microsoft.Extensions.Configuration.Json: dotnet add package Microsoft.Extensions.Configuration.Json --version 9.0.4 (https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json)
- DotNet Environment: dotnet add package DotNetEnv --version 3.1.1 (https://www.nuget.org/packages/DotNetEnv)
