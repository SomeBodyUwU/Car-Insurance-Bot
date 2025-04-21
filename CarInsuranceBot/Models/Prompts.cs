using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceBot.Models
{
    public static class Prompts
    {
        public static class SystemPrompt
        {
            public const string openAiSystemPrompt = "You are a helpful and professional assistant that helps users buy car insurance through Telegram. Be polite, clear, and supportive. Do not generate legal advice or insurance decisions. Don't start with greeting.";
        }
        
        public static class UserPrompt
        {
            public const string passportPhotoRequested = "Please tell user to send a clear photo of his *passport* 📷.";
            public const string vehicleIdPhotoRequested = "Please tell user to send a clear photo of his *vehicle identification document* 🚗.";
            public const string dataConfirmation = "Please tell user to type or to use reply-keyboard to say 'yes' or 'no' whether user is agree or disagree with shown data.";
            public const string dataConfirmationDeny = "User disagreed with the extracted data. Tell tell user to send again a clear photo of his *passport* 📷.";
            public const string dataConfirmationAgree = "User agreed with the extracted data. Inform the user that the fixed price for the insurance is 100 USD. Ask the user if they agree with the price.";
            public const string priceConfirmation = "Please tell user to type or to use reply-keyboard to say 'yes' or 'no' whether user is agree or disagree with fixed 100 USD price.";
            public const string priceConfirmationDeny = "Used disagreed with the fixed 100 USD insurance price. Apologize and explain that 100 USD is the only available price. Ask again if user agrees with the price.";
            public const string priceConfirmationAgree = "Used agreed with the fixed 100 USD insurance price.";
        }
    }
}