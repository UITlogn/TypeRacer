namespace TypeRacer.Server.Data;

public class DuplicateUsernameException : Exception
{
    public DuplicateUsernameException(string username, Exception? innerException = null)
        : base($"Username '{username}' already exists.", innerException)
    {
        Username = username;
    }

    public string Username { get; }
}
