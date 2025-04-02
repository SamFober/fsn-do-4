using System;

namespace WebApi.Models.Exceptions
{
    public class TicketNotFoundException : Exception
    {
        public TicketNotFoundException() : base("The requested tickets could not be found.") { }

        public TicketNotFoundException(string message) : base(message) { }

        public TicketNotFoundException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}