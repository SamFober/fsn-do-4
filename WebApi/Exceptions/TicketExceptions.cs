namespace WebApi.Exceptions;

public class NoSeatsAvailableException : Exception
{
    public NoSeatsAvailableException(string message) : base(message) { }
}

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(string message) : base(message) { }
}

public class SeatNotAvailableException : Exception
{
    public SeatNotAvailableException(string message) : base(message) { }
}

public class ConcessionNotFoundException : Exception
{
    public ConcessionNotFoundException(string message) : base(message) { }
}

public class TicketNotFoundException : Exception
{
    public TicketNotFoundException() : base("The requested tickets could not be found.") { }

    public TicketNotFoundException(string message) : base(message) { }

    public TicketNotFoundException(string message, Exception innerException)
        : base(message, innerException) { }
}