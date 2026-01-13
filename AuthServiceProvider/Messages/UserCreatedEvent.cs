namespace AuthServiceProvider.Messages
{
    public record UserCreatedEvent
    (
        string FirstName,
        string LastName,
        string Email,
        string Role
    );
}
