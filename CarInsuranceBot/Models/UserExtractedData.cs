using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceBot.Models
{
    public class UserExtractedData
    {
        private readonly string _Name;
        private readonly string _PassportNumber;
        private readonly string _VehicleNumber;

        public UserExtractedData(string name, string passportNumber, string vehicleNumber)
        {
            _Name = name;
            _PassportNumber = passportNumber;
            _VehicleNumber = vehicleNumber;
        }

        public string GetName()
        {
            return _Name;
        }

        public string GetPassportNumber()
        {
            return _PassportNumber;
        }

        public string GetVehicleNumber()
        {
            return _VehicleNumber;
        }
    }
}