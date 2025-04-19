using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarInsuranceBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CarInsuranceBot
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly CancellationTokenSource cst = new();
        private readonly ReceiverOptions receiverOptions = new() { AllowedUpdates = { } };
        private readonly Dictionary<long, UserState> userState = [];
        public enum UserState
        {
            None,
            AwaitingPassport,
            AwaitingVehicleDoc,
            Processing
        }
        public TelegramBotService()
        {
            _botClient = new TelegramBotClient("7193718569:AAEr_4uMVbAZq2n5iaWSb6DGEGUGPnMs_Gc");
        }
        public async Task Start()
        {
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions, cst.Token);
            var botItself = await _botClient.GetMe();
            Console.WriteLine($"Bot {botItself.Username} is running! Press any key to exit.");
            Console.ReadKey();
        }
        private async Task UpdateHandler(ITelegramBotClient _botClient, Update update, CancellationToken cst)
        {
            if(update.Message == null) return;
            var message = update.Message;
            var chatId = message.Chat.Id;
            if(!userState.ContainsKey(chatId))
            userState[chatId] = UserState.None;
            if(message.Type == MessageType.Text && message.Text?.ToLower() == "/start" && userState[chatId] == UserState.None)
            {
                userState[chatId] = UserState.AwaitingPassport;
                await _botClient.SendMessage(chatId, "👋 Hello! I'm your Car Insurance Assistant Bot.");
            }
            switch (userState[chatId])
            {
                case UserState.AwaitingPassport:
                    if (message.Type == MessageType.Photo)
                    {
                        userState[chatId] = UserState.AwaitingVehicleDoc;
                        await _botClient.SendMessage(chatId,
                            "✅ Passport photo received.\nNow send a photo of your *vehicle identification document* 🚗",
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    else
                    {
                        await _botClient.SendMessage(chatId, "Please send me a clear photo of your *passport* 📷");
                    }
                    break;

                case UserState.AwaitingVehicleDoc:
                    if (message.Type == MessageType.Photo)
                    {
                        userState[chatId] = UserState.Processing;
                        await _botClient.SendMessage(chatId,"✅ Vehicle document received.\nProcessing your information... ⏳");

                        var mindeeService = new MindeeService();
                        var extractedData = mindeeService.MindeeDataExtraction();

                        await _botClient.SendMessage(chatId, $"📝 Here’s what I found:\n" +
                            $"👤 Name: {extractedData.getName()}\n" +
                            $"🪪 Passport ID: {extractedData.getPassportNumber()}\n" +
                            $"🚘 Vehicle ID: {extractedData.getVehicleNumber()}\n\n" +
                            "Is this information correct? ✅");
                    }
                    else
                    {
                        await _botClient.SendMessage(chatId, "Please send me a clear photo of your *vehicle identification document* 🚗");
                    }
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