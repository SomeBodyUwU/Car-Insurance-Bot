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
    /// <summary>
    /// Service responsible for handling Telegram bot lifecycle, updates, and user interactions
    /// </summary>
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient; // Client for interacting with Telegram Bot API
        private OpenAiService _openAiService = new(); // Service for communicating with OpenAI
        private readonly CancellationTokenSource _cst = new(); // Token source for stopping the bot gracefully
        private readonly ReceiverOptions _receiverOptions = new() { AllowedUpdates = { } }; // Options to configure which updates to receive
        private readonly Dictionary<long, UserState> _userState = []; // Tracks each user's current state
        private readonly Dictionary<long, UserExtractedData> _extractedDataByUser = []; // Caches extracted data per user

        /// <summary>
        /// Represents the possible states a user can be in during the conversation
        /// </summary>
        public enum UserState
        {
            None,
            AwaitingPassport,
            AwaitingVehicleDoc,
            Processing,
            AwaitingMoneyConfirmation,
            Finished
        }

        /// <summary>
        /// Initializes the TelegramBotClient with the API key from environment variables
        /// </summary>
        public TelegramBotService()
        {
            var telegramApiKey = Environment.GetEnvironmentVariable("CARINSURANCEBOT_TELEGRAM_API_KEY")
                ?? throw new InvalidOperationException("Telegram API key is missing from environment variables.");
            _botClient = new TelegramBotClient(telegramApiKey);
        }

        /// <summary>
        /// Starts the bot: begins receiving updates and prints bot info
        /// </summary>
        public async Task Start()
        {
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, _cst.Token);
            var botItself = await _botClient.GetMe();
            Console.WriteLine($"Bot {botItself.Username} is running! Press any key to exit.");
            await Task.Delay(-1);
        }

        /// <summary>
        /// Handles incoming updates (messages) from Telegram
        /// </summary>
        private async Task UpdateHandler(ITelegramBotClient _botClient, Update update, CancellationToken cst)
        {
            if(update.Message == null) return;

            var message = update.Message;
            var chatId = message.Chat.Id;

            // Initialize state for new users
            if(!_userState.ContainsKey(chatId)) _userState[chatId] = UserState.None;

            // Handle /start command
            if(message.Type == MessageType.Text && message.Text?.ToLower() == "/start")
            {
                _userState[chatId] = UserState.AwaitingPassport;
                await _botClient.SendMessage(chatId, "👋 Hello! I'm your Car Insurance Assistant Bot.", replyMarkup: new ReplyKeyboardRemove());
            }
            switch (_userState[chatId])
            {
                case UserState.AwaitingPassport:
                    await HandleAwaitingPassportAsync(message, chatId);
                    break;
                case UserState.AwaitingVehicleDoc:
                    await HandleAwaitingVehicleDocAsync(message, chatId);          
                    break;
                case UserState.Processing:
                    await HandleProcessingAsync(message, chatId);
                    break;
                case UserState.AwaitingMoneyConfirmation:
                    await HandleMoneyConfirmationAsync(message, chatId);
                    break;
                case UserState.Finished:
                    // Inform user the process is complete
                    await _botClient.SendMessage(chatId, "Thank you for using our service. If you want to have one more insurance please type /start");
                    break;

            }
        }

        #region State Handlers

        /// <summary>
        /// Handles messages when the bot is waiting for a passport photo
        /// </summary>
        private async Task HandleAwaitingPassportAsync(Message message, long chatId)
        {
            if (message.Type == MessageType.Photo)
            {
                // Transition to next state
                _userState[chatId] = UserState.AwaitingVehicleDoc;
                await _botClient.SendMessage(chatId,
                    "✅ Passport photo received.\nNow send a photo of your *vehicle identification document* 🚗");
            }
            else
            {
                // Prompt user to send a passport photo via OpenAI prompt
                var response = await _openAiService.ChatGPTResponse(
                    Prompts.SystemPrompt.openAiSystemPrompt,
                    Prompts.UserPrompt.passportPhotoRequested
                );

                await _botClient.SendMessage(chatId, response);
            }
        }

        /// <summary>
        /// Handles messages when the bot is waiting for a vehicle document photo
        /// </summary>
        private async Task HandleAwaitingVehicleDocAsync(Message message, long chatId)
        {
            if (message.Type == MessageType.Photo)
            {
                _userState[chatId] = UserState.Processing;
                await _botClient.SendMessage(chatId,"✅ Vehicle document received.\nProcessing your information... ⏳");

                // Extract data with Mindee and cache it
                var mindeeService = new MindeeService();
                var extractedData = mindeeService.MindeeDataExtraction();
                _extractedDataByUser[chatId] = extractedData;
                var replyKeyboard = CreateReplyKeyboard();

                // Show extracted data and ask for confirmation
                await _botClient.SendMessage(chatId, SendExtractedData(extractedData), replyMarkup: replyKeyboard);
            }
            else
            {
                // Prompt user to send vehicle document
                var response = await _openAiService.ChatGPTResponse(
                    Prompts.SystemPrompt.openAiSystemPrompt,
                    Prompts.UserPrompt.vehicleIdPhotoRequested
                );

                await _botClient.SendMessage(chatId, response);
            }
        }

        /// <summary>
        /// Handles confirmation of extracted data (Yes/No)
        /// </summary>
        private async Task HandleProcessingAsync(Message message, long chatId)
        {
            if (message.Type == MessageType.Text)
            {
                var text = message.Text?.Trim().ToLower();

                // Process user confirmation
                switch (text)
                {
                    case "yes":
                        _userState[chatId] = UserState.AwaitingMoneyConfirmation;
                        var agreeResponse = await _openAiService.ChatGPTResponse(
                        Prompts.SystemPrompt.openAiSystemPrompt,
                        Prompts.UserPrompt.dataConfirmationAgree
                        );

                        await _botClient.SendMessage(chatId, agreeResponse);
                        break;
                    case "no":
                        _userState[chatId] = UserState.AwaitingPassport;
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
                // Ask user again to confirm on deny extracted data
                var defaultResponse = await _openAiService.ChatGPTResponse(
                Prompts.SystemPrompt.openAiSystemPrompt,
                Prompts.UserPrompt.dataConfirmation
                );

                await _botClient.SendMessage(chatId, defaultResponse);
            }
        }

        /// <summary>
        /// Handles user confirmation for the insurance price (Yes/No)
        /// </summary>
        private async Task HandleMoneyConfirmationAsync(Message message, long chatId)
        {
            if (message.Type == MessageType.Text)
            {
                var text = message.Text?.Trim().ToLower();

                // Process user confirmation
                switch (text)
                {
                    case "yes":
                        _userState[chatId] = UserState.Finished;
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
                            // Session expired fallback
                            await _botClient.SendMessage(chatId, "❌ Sorry, your session has expired. Please restart.", replyMarkup: new ReplyKeyboardRemove());
                            _userState[chatId] = UserState.AwaitingPassport;
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
                // Ask user again to accept or deny price
                var defaultResponse = await _openAiService.ChatGPTResponse(
                Prompts.SystemPrompt.openAiSystemPrompt,
                Prompts.UserPrompt.priceConfirmation
                );

                await _botClient.SendMessage(chatId, defaultResponse);
            }
        }

        #endregion

        /// <summary>
        /// Creates a simple Yes/No reply keyboard
        /// </summary>
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

        /// <summary>
        /// Formats the extracted data into a confirmation message
        /// </summary>
        private string SendExtractedData(UserExtractedData extractedData)
        {
            return  $"📝 Here’s what I found:\n" +
                    $"👤 Name: {extractedData.GetName()}\n" +
                    $"🪪 Passport ID: {extractedData.GetPassportNumber()}\n" +
                    $"🚘 Vehicle ID: {extractedData.GetVehicleNumber()}\n\n" +
                    "Is this information correct? (please use in-build keyboard)✅";
        }

        /// <summary>
        /// Reads and fills the insurance template file with user data
        /// </summary>
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

        /// <summary>
        /// Handles errors from the Telegram bot and attempts to restart if not canceled
        /// </summary>
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

                    _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cst);
                }
                catch (Exception restartException)
                {
                    Console.WriteLine($"An error occurred while restarting the bot: {restartException.Message}");
                }
            }
        }
    }
}