using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CarInsuranceBot.Models;
using CarInsuranceBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CarInsuranceBot
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private OpenAiService _openAiService = new();
        private readonly CancellationTokenSource cst = new();
        private readonly ReceiverOptions receiverOptions = new() { AllowedUpdates = { } };
        private readonly Dictionary<long, UserState> userState = [];
        private readonly Dictionary<long, UserExtractedData> _extractedDataByUser = [];

        public enum UserState
        {
            None,
            AwaitingPassport,
            AwaitingVehicleDoc,
            Processing,
            AwaitingMoneyConfirmation,
            Finished
        }
        public TelegramBotService()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Templates", "prompts.json");
            var json = File.ReadAllText(path);
            var telegramApiKey = Environment.GetEnvironmentVariable("CARINSURANCEBOT_TELEGRAM_API_KEY")
                ?? throw new InvalidOperationException("Telegram API key is missing from environment variables.");
            _botClient = new TelegramBotClient(telegramApiKey);
        }
        public async Task Start()
        {
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions, cst.Token);
            var botItself = await _botClient.GetMe();
            Console.WriteLine($"Bot {botItself.Username} is running! Press any key to exit.");
            await Task.Delay(-1);
        }

        private ReplyKeyboardMarkup CreateReplyKeyboard()
        {
            return new ReplyKeyboardMarkup(
                new List<KeyboardButton[]>()
                {
                    new KeyboardButton[]
                    {
                        new("Yes"),
                        new("No"),
                    }
                })
            {
                ResizeKeyboard = true,
            };
        }

        private string SendExtractedData(UserExtractedData extractedData)
        {
            var message = $"📝 Here’s what I found:\n" +
                        $"👤 Name: {extractedData.GetName()}\n" +
                        $"🪪 Passport ID: {extractedData.GetPassportNumber()}\n" +
                        $"🚘 Vehicle ID: {extractedData.GetVehicleNumber()}\n\n" +
                        "Is this information correct? (please use in-build keyboard)✅";
            return message;
        }
        private string FillInsurancePromptTemplate(UserExtractedData extractedData)
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "insurance_template_openAi_prompt.txt");
            var templateText = File.ReadAllText(templatePath);
            var filled = templateText
            .Replace("{name}", extractedData.GetName())
            .Replace("{passportId}", extractedData.GetPassportNumber())
            .Replace("{vehicleId}", extractedData.GetVehicleNumber());

            return filled;
        }

        private async Task UpdateHandler(ITelegramBotClient _botClient, Update update, CancellationToken cst)
        {
            if(update.Message == null) return;
            var message = update.Message;
            var chatId = message.Chat.Id;
            if(!userState.ContainsKey(chatId)) userState[chatId] = UserState.None;
            if(message.Type == MessageType.Text && message.Text?.ToLower() == "/start")
            {
                userState[chatId] = UserState.AwaitingPassport;
                await _botClient.SendMessage(chatId, "👋 Hello! I'm your Car Insurance Assistant Bot.", replyMarkup: new ReplyKeyboardRemove());
            }
            switch (userState[chatId])
            {
                case UserState.AwaitingPassport:
                    if (message.Type == MessageType.Photo)
                    {
                        userState[chatId] = UserState.AwaitingVehicleDoc;
                        await _botClient.SendMessage(chatId,
                            "✅ Passport photo received.\nNow send a photo of your *vehicle identification document* 🚗");
                    }
                    else
                    {
                        var response = await _openAiService.ChatGPTResponse(
                            Prompts.SystemPrompt.openAiSystemPrompt,
                            Prompts.UserPrompt.passportPhotoRequested
                        );

                        await _botClient.SendMessage(chatId, response);
                    }
                    break;

                case UserState.AwaitingVehicleDoc:
                    if (message.Type == MessageType.Photo)
                    {
                        userState[chatId] = UserState.Processing;
                        await _botClient.SendMessage(chatId,"✅ Vehicle document received.\nProcessing your information... ⏳");

                        var mindeeService = new MindeeService();
                        var extractedData = mindeeService.MindeeDataExtraction();
                        _extractedDataByUser[chatId] = extractedData;
                        var replyKeyboard = CreateReplyKeyboard();

                        await _botClient.SendMessage(chatId, SendExtractedData(extractedData), replyMarkup: replyKeyboard);
                    }
                    else
                    {
                        var response = await _openAiService.ChatGPTResponse(
                            Prompts.SystemPrompt.openAiSystemPrompt,
                            Prompts.UserPrompt.vehicleIdPhotoRequested
                        );

                        await _botClient.SendMessage(chatId, response);
                    }
                    break;

                case UserState.Processing:
                    if (message.Type == MessageType.Text)
                    {
                        var text = message.Text?.Trim().ToLower();
                        switch (text)
                        {
                            case "yes":
                                userState[chatId] = UserState.AwaitingMoneyConfirmation;
                                var agreeResponse = await _openAiService.ChatGPTResponse(
                                Prompts.SystemPrompt.openAiSystemPrompt,
                                Prompts.UserPrompt.dataConfirmationAgree
                                );

                                await _botClient.SendMessage(chatId, agreeResponse);
                                break;
                            case "no":
                                userState[chatId] = UserState.AwaitingPassport;
                                var denyResponse = await _openAiService.ChatGPTResponse(
                                Prompts.SystemPrompt.openAiSystemPrompt,
                                Prompts.UserPrompt.dataConfirmationDeny
                                );

                                await _botClient.SendMessage(chatId, denyResponse, replyMarkup: new ReplyKeyboardRemove());
                                break;
                            default:
                                var defaultResponse = await _openAiService.ChatGPTResponse(
                                Prompts.SystemPrompt.openAiSystemPrompt,
                                Prompts.UserPrompt.dataConfirmation
                                );

                                await _botClient.SendMessage(chatId, defaultResponse);
                                break;
                        }
                    }
                    else
                    {
                        var defaultResponse = await _openAiService.ChatGPTResponse(
                        Prompts.SystemPrompt.openAiSystemPrompt,
                        Prompts.UserPrompt.dataConfirmation
                        );

                        await _botClient.SendMessage(chatId, defaultResponse);
                        break;
                    }
                    break;
                case UserState.AwaitingMoneyConfirmation:
                    if (message.Type == MessageType.Text)
                    {
                        var text = message.Text?.Trim().ToLower();
                        switch (text)
                        {
                            case "yes":
                                userState[chatId] = UserState.Finished;
                                if(_extractedDataByUser.TryGetValue(chatId, out var extractedData))
                                {
                                    var prompt = FillInsurancePromptTemplate(extractedData);
                                    var agreeResponse = await _openAiService.ChatGPTResponse(
                                        Prompts.SystemPrompt.openAiSystemPrompt,
                                        prompt
                                    );

                                    await _botClient.SendMessage(chatId, agreeResponse, replyMarkup: new ReplyKeyboardRemove());
                                }
                                else
                                {
                                    await _botClient.SendMessage(chatId, "❌ Sorry, your session has expired. Please restart.", replyMarkup: new ReplyKeyboardRemove());
                                    userState[chatId] = UserState.AwaitingPassport;
                                }
                                break;
                            case "no":
                                var denyResponse = await _openAiService.ChatGPTResponse(
                                Prompts.SystemPrompt.openAiSystemPrompt,
                                Prompts.UserPrompt.priceConfirmationDeny
                                );

                                await _botClient.SendMessage(chatId, denyResponse);
                                break;
                            default:
                                var defaultResponse = await _openAiService.ChatGPTResponse(
                                Prompts.SystemPrompt.openAiSystemPrompt,
                                Prompts.UserPrompt.priceConfirmation
                                );

                                await _botClient.SendMessage(chatId, defaultResponse);
                                break;
                        }
                    }
                    else
                    {
                        var defaultResponse = await _openAiService.ChatGPTResponse(
                        Prompts.SystemPrompt.openAiSystemPrompt,
                        Prompts.UserPrompt.priceConfirmation
                        );

                        await _botClient.SendMessage(chatId, defaultResponse);
                        break;
                    }
                    break;
                case UserState.Finished:
                    await _botClient.SendMessage(chatId, "Thank you for using our service. If you want to have one more insurance please type /start");
                    break;

            }
        }
        private async Task ErrorHandler(ITelegramBotClient _botclient, Exception exception, CancellationToken cst)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"An error occured in telegram bot API:\n{apiRequestException.ErrorCode}\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            if (!cst.IsCancellationRequested)
            {
                try
                {
                    
                    await Task.Delay(1000, cst);

                    _botClient.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions, cst);
                }
                catch (Exception restartException)
                {
                    Console.WriteLine($"An error occurred while restarting the bot: {restartException.Message}");
                }
            }
        }
    }
}