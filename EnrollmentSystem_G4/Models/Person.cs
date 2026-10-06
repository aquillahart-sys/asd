namespace EnrollmentSystem_G4.Models { 
    public abstract class Person
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string GetFullName()
        {
            return $"{FirstName} {LastName}";
        }
    }
}