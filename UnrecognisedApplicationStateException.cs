using System;

public class UnrecognisedApplicationStateException : Exception
{
    public UnrecognisedApplicationStateException()
    {
    }

    public UnrecognisedApplicationStateException(string message)
        : base(message)
    {
    }

    public UnrecognisedApplicationStateException(string message, Exception inner)
        : base(message, inner)
    {
    }
}