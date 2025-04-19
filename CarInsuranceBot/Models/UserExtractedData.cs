using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceBot.Models
{
    public class UserExtractedData
    {
        private readonly string Name;
        private readonly string PassportNumber;
        private readonly string VehicleNumber;

        public UserExtractedData(string name, string passportNumber, string vehicleNumber)
        {
            Name = name;
            PassportNumber = passportNumber;
            VehicleNumber = vehicleNumber;
        }

        public string GetName()
        {
            return Name;
        }

        public string GetPassportNumber()
        {
            return PassportNumber;
        }

        public string GetVehicleNumber()
        {
            return VehicleNumber;
        }
    }
}