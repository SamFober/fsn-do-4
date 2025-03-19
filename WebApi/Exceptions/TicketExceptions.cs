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