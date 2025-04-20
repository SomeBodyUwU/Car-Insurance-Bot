using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceBot.Models
{
    public class Prompts
    {
        public Dictionary<string, string> systemPrompt {get; set;}
        public Dictionary<string, string> userPrompt {get; set;}
    }
}