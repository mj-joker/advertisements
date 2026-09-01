namespace Avatisment.Models;

public enum NotificationType { Like, Comment, Follow }

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RecipientUserId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string? PostId { get; set; }
    public string? PostExcerpt { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public bool Read { get; set; }

    public AppUser? Actor { get; set; }
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FromUserId { get; set; } = string.Empty;
    public string ToUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
