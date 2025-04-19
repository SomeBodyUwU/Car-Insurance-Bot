using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarInsuranceBot.Models;

namespace CarInsuranceBot.Services
{
    public class MindeeService
    {
        public UserExtractedData MindeeDataExtraction()
        {
            return new UserExtractedData("John Doe","AVF63HVE7345GED","AA 4573 AD");
        }
    }
}