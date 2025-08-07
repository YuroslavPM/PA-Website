using System;
using System.ComponentModel.DataAnnotations;

namespace PA_Website.Attributes
{
    public class MinimumAgeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;

        public MinimumAgeAttribute(int minimumAge)
        {
            _minimumAge = minimumAge;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime birthDate)
            {
                if (birthDate > DateTime.Today)
                {
                    return new ValidationResult("Датата на раждане не може да бъде в бъдещето");
                }

                var age = DateTime.Today.Year - birthDate.Year;
                
                // Adjust age if birthday hasn't occurred yet this year
                if (birthDate > DateTime.Today.AddYears(-age))
                {
                    age--;
                }

                if (age < _minimumAge)
                {
                    return new ValidationResult($"Трябва да сте навършили поне {_minimumAge} години");
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Невалидна дата на раждане");
        }
    }
}