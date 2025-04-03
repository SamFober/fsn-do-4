namespace WebApi.Models.Exceptions
{
    public class MollieApiKeyNotSetException : Exception
    {
        public MollieApiKeyNotSetException(string message) : base(message) { }
        public MollieApiKeyNotSetException() 
            : base("Mollie API key not set. Please add your Mollie API key to appsettings.json") { }
    }
}
