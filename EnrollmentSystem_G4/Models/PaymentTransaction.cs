namespace EnrollmentSystem_G4.Models
{
    public abstract class PaymentTransaction
    {
        public decimal Amount { get; set; }

        public PaymentTransaction(decimal amount)
        {
            Amount = amount;
        }

        public abstract string ProcessPayment();

        public string FormatAmount()
        {
            return Amount.ToString("C");
        }
    }
}