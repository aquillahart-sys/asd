namespace EnrollmentSystem_G4.Models
{
    public interface IPaymentProcessor
    {
        string ProcessPayment(decimal amount);
    }

    public class DownpaymentProcessor : IPaymentProcessor
    {
        public string ProcessPayment(decimal amount)
        {
            return $"[Downpayment Processed]: Applied initial payment of {amount:C}.";
        }
    }

    public class MonthlyPaymentProcessor : IPaymentProcessor
    {
        public string ProcessPayment(decimal amount)
        {
            return $"[Monthly Installment Processed]: Applied payment of {amount:C}.";
        }
    }

    public class FullPaymentProcessor : IPaymentProcessor
    {
        public string ProcessPayment(decimal amount)
        {
            return $"[Full Payment Processed]: Settled full account balance with {amount:C}.";
        }
    }
}