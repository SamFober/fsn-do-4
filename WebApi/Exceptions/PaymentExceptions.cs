namespace WebApi.Exceptions
{
    public class PaymentAlreadyExistsException : Exception
    {
        public PaymentAlreadyExistsException(string message) : base(message) { }
        public PaymentAlreadyExistsException() : base("Payment already exists for this order") { }
    }

    public class PaymentNotFoundException : Exception
    {
        public PaymentNotFoundException(string message) : base(message) { }
        public PaymentNotFoundException() : base("Payment already exists for this order") { }
    }

    public class MollieApiKeyNotSetException : Exception
    {
        public MollieApiKeyNotSetException(string message) : base(message) { }
        public MollieApiKeyNotSetException()
            : base("Mollie API key not set. Please add your Mollie API key to appsettings.json") { }
    }
}
