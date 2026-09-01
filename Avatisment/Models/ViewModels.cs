using System.ComponentModel.DataAnnotations;

namespace Avatisment.Models;

public class FeedViewModel
{
    public AppUser? CurrentUser { get; set; }
    public List<Post> Posts { get; set; } = new();
    public List<AppUser> SuggestedUsers { get; set; } = new();
    public List<(string Tag, int Count)> TrendingTags { get; set; } = new();
}

public class ProfileViewModel
{
    public AppUser? CurrentUser { get; set; }
    public AppUser ProfileUser { get; set; } = null!;
    public List<Post> Posts { get; set; } = new();
    public bool IsFollowing { get; set; }
    public bool IsOwnProfile { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(40, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20, MinimumLength = 7, ErrorMessage = "Enter a valid phone number.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Error { get; set; }
}

public class VerifyCodeViewModel
{
    [Required, StringLength(6, MinimumLength = 6, ErrorMessage = "Enter the 6-digit code.")]
    public string Code { get; set; } = string.Empty;

    public string? Error { get; set; }
    public string? Destination { get; set; }   // masked email or phone shown to the user

    // Demo-mode only: since there's no real email/SMS provider wired up,
    // the generated code is surfaced directly on the page so the flow is testable end to end.
    public string? DemoCode { get; set; }
}

public class CreatePostRequest
{
    public string Content { get; set; } = string.Empty;
    public bool IsReel { get; set; }
}

public class CommentRequest
{
    public string PostId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ExploreViewModel
{
    public AppUser? CurrentUser { get; set; }
    public string? Query { get; set; }
    public List<AppUser> MatchingUsers { get; set; } = new();
    public List<Post> MatchingPosts { get; set; } = new();
    public List<AppUser> DiscoverPeople { get; set; } = new();
    public List<Post> TrendingPosts { get; set; } = new();
    public List<(string Tag, int Count)> TrendingTags { get; set; } = new();
}

public class NotificationsViewModel
{
    public AppUser? CurrentUser { get; set; }
    public List<Notification> Notifications { get; set; } = new();
}

public class MessagesViewModel
{
    public AppUser? CurrentUser { get; set; }
    public List<(AppUser Partner, ChatMessage? LastMessage)> Conversations { get; set; } = new();
    public AppUser? ActivePartner { get; set; }
    public List<ChatMessage> Thread { get; set; } = new();
}
