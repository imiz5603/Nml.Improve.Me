using System;

public class NoApplicationFoundException : Exception
{
    public NoApplicationFoundException()
    {
    }

    public NoApplicationFoundException(string message)
        : base(message)
    {
    }

    public NoApplicationFoundException(string message, Exception inner)
        : base(message, inner)
    {
    }
}