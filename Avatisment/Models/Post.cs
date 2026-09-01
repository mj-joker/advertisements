namespace Avatisment.Models;

public enum PostType { Post, Reel }

public class Post
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public PostType Type { get; set; } = PostType.Post;
    public string? ImageGradient { get; set; }     // decorative gradient used instead of real uploads
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public List<string> LikedByUserIds { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public int ShareCount { get; set; }

    // Populated by the controller before rendering — not persisted.
    public AppUser? Author { get; set; }
    public bool LikedByCurrentUser { get; set; }
}

public class Comment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AuthorId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public AppUser? Author { get; set; }
}
