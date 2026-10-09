namespace Meetrec.Core;

// An error the user can act on: the CLI prints its message without a stack trace.
public class MeetrecException(string message, Exception? innerException = null) : Exception(message, innerException);
