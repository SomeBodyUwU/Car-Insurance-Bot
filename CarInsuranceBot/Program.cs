using CarInsuranceBot;

class Program
{
    public static void Main(string[] args)
    {
        TelegramBotService insuranceBot = new TelegramBotService();
        insuranceBot.Start().GetAwaiter().GetResult();
    }
}