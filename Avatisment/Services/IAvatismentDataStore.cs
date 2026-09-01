using Avatisment.Models;

namespace Avatisment.Services;

public interface IAvatismentDataStore
{
    // Users
    AppUser? GetUserById(string id);
    AppUser? GetUserByEmail(string email);
    AppUser? GetUserByHandle(string handle);
    AppUser? GetUserByPhone(string phone);
    AppUser CreateUser(string displayName, string handle, string email, string phone, string passwordHash);
    IEnumerable<AppUser> SuggestedUsersFor(string currentUserId, int take = 4);
    void ToggleFollow(string currentUserId, string targetUserId);

    // Verification
    string GenerateEmailCode(string userId);
    string GeneratePhoneCode(string userId);
    bool VerifyEmailCode(string userId, string code);
    bool VerifyPhoneCode(string userId, string code);

    // Login security (brute-force protection)
    bool IsLockedOut(string email, out TimeSpan? remaining);
    void RegisterFailedLogin(string email);
    void ClearFailedLogins(string email);

    // Posts
    IEnumerable<Post> GetFeed(string? currentUserId);
    IEnumerable<Post> GetPostsByUser(string userId, string? currentUserId);
    Post CreatePost(string authorId, string content, string? gradient, bool isReel = false);
    void ToggleLike(string postId, string userId);
    Comment AddComment(string postId, string authorId, string content);
    IEnumerable<(string Tag, int Count)> TrendingTags();
    IEnumerable<AppUser> SearchUsers(string query);
    IEnumerable<Post> SearchPosts(string query, string? currentUserId);
    IEnumerable<Post> TrendingPosts(string? currentUserId, int take = 8);

    // Notifications
    IEnumerable<Notification> GetNotifications(string userId);
    void MarkAllNotificationsRead(string userId);

    // Messages
    IEnumerable<(AppUser Partner, ChatMessage? LastMessage)> GetConversations(string userId);
    IEnumerable<ChatMessage> GetThread(string userId, string otherUserId);
    ChatMessage SendMessage(string fromUserId, string toUserId, string content);
}
