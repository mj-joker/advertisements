using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Avatisment.Models;

namespace Avatisment.Services;

/// <summary>
/// Thread-safe in-memory store so the app runs with zero external dependencies.
/// Replace with an EF Core DbContext-backed implementation for production use.
/// </summary>
public class InMemoryDataStore : IAvatismentDataStore
{
    private readonly ConcurrentDictionary<string, AppUser> _users = new();
    private readonly ConcurrentDictionary<string, Post> _posts = new();
    private readonly ConcurrentBag<Notification> _notifications = new();
    private readonly ConcurrentBag<ChatMessage> _messages = new();
    private static readonly string[] Gradients =
    {
        "linear-gradient(135deg,#7C5CFC,#22D3EE)",
        "linear-gradient(135deg,#FF6B9D,#FFA36B)",
        "linear-gradient(135deg,#22D3EE,#3AF0B7)",
        "linear-gradient(135deg,#FFB020,#FF6B6B)",
        "linear-gradient(135deg,#8E7CFF,#5CC8FC)",
    };

    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly Regex TagStripper = new("<.*?>", RegexOptions.Compiled);

    public InMemoryDataStore()
    {
        Seed();
    }

    // ---------- Users ----------

    public AppUser? GetUserById(string id) => _users.GetValueOrDefault(id);

    public AppUser? GetUserByEmail(string email) =>
        _users.Values.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public AppUser? GetUserByHandle(string handle) =>
        _users.Values.FirstOrDefault(u => u.Handle.Equals(handle, StringComparison.OrdinalIgnoreCase));

    public AppUser? GetUserByPhone(string phone) =>
        _users.Values.FirstOrDefault(u => u.PhoneNumber == NormalizePhone(phone));

    public AppUser CreateUser(string displayName, string handle, string email, string phone, string passwordHash)
    {
        var user = new AppUser
        {
            DisplayName = displayName,
            Handle = handle,
            Email = email.Trim(),
            PhoneNumber = NormalizePhone(phone),
            PasswordHash = passwordHash,
            AvatarColor = Gradients[Random.Shared.Next(Gradients.Length)],
            CoverGradient = Gradients[Random.Shared.Next(Gradients.Length)],
            Bio = "New to Avatisment 👋"
        };
        _users[user.Id] = user;
        return user;
    }

    public IEnumerable<AppUser> SuggestedUsersFor(string currentUserId, int take = 4) =>
        _users.Values
            .Where(u => u.Id != currentUserId)
            .OrderBy(_ => Guid.NewGuid())
            .Take(take);

    public void ToggleFollow(string currentUserId, string targetUserId)
    {
        if (currentUserId == targetUserId) return;
        if (!_users.TryGetValue(currentUserId, out var me) || !_users.TryGetValue(targetUserId, out var them))
            return;

        if (me.FollowingIds.Contains(targetUserId))
        {
            me.FollowingIds.Remove(targetUserId);
            them.FollowerIds.Remove(currentUserId);
        }
        else
        {
            me.FollowingIds.Add(targetUserId);
            them.FollowerIds.Add(currentUserId);
            _notifications.Add(new Notification
            {
                RecipientUserId = targetUserId,
                ActorUserId = currentUserId,
                Type = NotificationType.Follow
            });
        }
    }

    // ---------- Verification ----------

    public string GenerateEmailCode(string userId)
    {
        var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        if (_users.TryGetValue(userId, out var user))
        {
            user.EmailVerificationCode = code;
            user.EmailCodeExpiresOn = DateTime.UtcNow.Add(CodeLifetime);
        }
        return code;
    }

    public string GeneratePhoneCode(string userId)
    {
        var code = Random.Shared.Next(0, 1_000_000).ToString("D6");
        if (_users.TryGetValue(userId, out var user))
        {
            user.PhoneVerificationCode = code;
            user.PhoneCodeExpiresOn = DateTime.UtcNow.Add(CodeLifetime);
        }
        return code;
    }

    public bool VerifyEmailCode(string userId, string code)
    {
        if (!_users.TryGetValue(userId, out var user)) return false;
        if (user.EmailVerificationCode is null || user.EmailCodeExpiresOn is null) return false;
        if (DateTime.UtcNow > user.EmailCodeExpiresOn) return false;
        if (user.EmailVerificationCode != code.Trim()) return false;

        user.EmailVerified = true;
        user.EmailVerificationCode = null;
        return true;
    }

    public bool VerifyPhoneCode(string userId, string code)
    {
        if (!_users.TryGetValue(userId, out var user)) return false;
        if (user.PhoneVerificationCode is null || user.PhoneCodeExpiresOn is null) return false;
        if (DateTime.UtcNow > user.PhoneCodeExpiresOn) return false;
        if (user.PhoneVerificationCode != code.Trim()) return false;

        user.PhoneVerified = true;
        user.PhoneVerificationCode = null;
        return true;
    }

    // ---------- Login security ----------

    public bool IsLockedOut(string email, out TimeSpan? remaining)
    {
        remaining = null;
        var user = GetUserByEmail(email);
        if (user?.LockedOutUntil is null) return false;

        if (DateTime.UtcNow >= user.LockedOutUntil)
        {
            user.LockedOutUntil = null;
            user.FailedLoginAttempts = 0;
            return false;
        }

        remaining = user.LockedOutUntil - DateTime.UtcNow;
        return true;
    }

    public void RegisterFailedLogin(string email)
    {
        var user = GetUserByEmail(email);
        if (user is null) return;

        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedLogins)
        {
            user.LockedOutUntil = DateTime.UtcNow.Add(LockoutDuration);
        }
    }

    public void ClearFailedLogins(string email)
    {
        var user = GetUserByEmail(email);
        if (user is null) return;
        user.FailedLoginAttempts = 0;
        user.LockedOutUntil = null;
    }

    // ---------- Posts ----------

    public IEnumerable<Post> GetFeed(string? currentUserId)
    {
        return _posts.Values
            .OrderByDescending(p => p.CreatedOn)
            .Select(p => Hydrate(p, currentUserId));
    }

    public IEnumerable<Post> GetPostsByUser(string userId, string? currentUserId)
    {
        return _posts.Values
            .Where(p => p.AuthorId == userId)
            .OrderByDescending(p => p.CreatedOn)
            .Select(p => Hydrate(p, currentUserId));
    }

    public Post CreatePost(string authorId, string content, string? gradient, bool isReel = false)
    {
        var clean = SanitizeContent(content, isReel ? 150 : 280);
        var post = new Post
        {
            AuthorId = authorId,
            Content = clean,
            Type = isReel ? PostType.Reel : PostType.Post,
            ImageGradient = gradient
        };
        _posts[post.Id] = post;
        return Hydrate(post, authorId);
    }

    public void ToggleLike(string postId, string userId)
    {
        if (!_posts.TryGetValue(postId, out var post)) return;
        if (!post.LikedByUserIds.Remove(userId))
        {
            post.LikedByUserIds.Add(userId);
            if (post.AuthorId != userId)
            {
                _notifications.Add(new Notification
                {
                    RecipientUserId = post.AuthorId,
                    ActorUserId = userId,
                    Type = NotificationType.Like,
                    PostId = post.Id,
                    PostExcerpt = Excerpt(post.Content)
                });
            }
        }
    }

    public Comment AddComment(string postId, string authorId, string content)
    {
        var clean = SanitizeContent(content, 200);
        var comment = new Comment { AuthorId = authorId, Content = clean };
        if (_posts.TryGetValue(postId, out var post))
        {
            post.Comments.Add(comment);
            if (post.AuthorId != authorId)
            {
                _notifications.Add(new Notification
                {
                    RecipientUserId = post.AuthorId,
                    ActorUserId = authorId,
                    Type = NotificationType.Comment,
                    PostId = post.Id,
                    PostExcerpt = Excerpt(post.Content)
                });
            }
        }
        comment.Author = GetUserById(authorId);
        return comment;
    }

    public IEnumerable<(string Tag, int Count)> TrendingTags()
    {
        return _posts.Values
            .SelectMany(p => p.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.StartsWith('#') && w.Length > 1)
            .GroupBy(w => w.Trim('.', ',', '!', '?'))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => (g.Key, g.Count()));
    }

    public IEnumerable<AppUser> SearchUsers(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<AppUser>();
        var q = query.Trim();
        return _users.Values.Where(u =>
            u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            u.Handle.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<Post> SearchPosts(string query, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<Post>();
        var q = query.Trim();
        return _posts.Values
            .Where(p => p.Content.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.CreatedOn)
            .Select(p => Hydrate(p, currentUserId));
    }

    public IEnumerable<Post> TrendingPosts(string? currentUserId, int take = 8)
    {
        return _posts.Values
            .OrderByDescending(p => p.LikedByUserIds.Count + p.Comments.Count)
            .Take(take)
            .Select(p => Hydrate(p, currentUserId));
    }

    // ---------- Notifications ----------

    public IEnumerable<Notification> GetNotifications(string userId)
    {
        return _notifications
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedOn)
            .Select(n => { n.Actor = GetUserById(n.ActorUserId); return n; });
    }

    public void MarkAllNotificationsRead(string userId)
    {
        foreach (var n in _notifications.Where(n => n.RecipientUserId == userId))
            n.Read = true;
    }

    // ---------- Messages ----------

    public IEnumerable<(AppUser Partner, ChatMessage? LastMessage)> GetConversations(string userId)
    {
        var partnerIds = _messages
            .Where(m => m.FromUserId == userId || m.ToUserId == userId)
            .Select(m => m.FromUserId == userId ? m.ToUserId : m.FromUserId)
            .Distinct();

        foreach (var pid in partnerIds)
        {
            var partner = GetUserById(pid);
            if (partner is null) continue;
            var last = _messages
                .Where(m => (m.FromUserId == userId && m.ToUserId == pid) || (m.FromUserId == pid && m.ToUserId == userId))
                .OrderByDescending(m => m.CreatedOn)
                .FirstOrDefault();
            yield return (partner, last);
        }
    }

    public IEnumerable<ChatMessage> GetThread(string userId, string otherUserId)
    {
        return _messages
            .Where(m => (m.FromUserId == userId && m.ToUserId == otherUserId) || (m.FromUserId == otherUserId && m.ToUserId == userId))
            .OrderBy(m => m.CreatedOn);
    }

    public ChatMessage SendMessage(string fromUserId, string toUserId, string content)
    {
        var clean = SanitizeContent(content, 500);
        var msg = new ChatMessage { FromUserId = fromUserId, ToUserId = toUserId, Content = clean };
        _messages.Add(msg);
        return msg;
    }

    private static string Excerpt(string content) =>
        content.Length <= 60 ? content : content[..57] + "…";

    // ---------- helpers ----------

    /// <summary>
    /// Server-side content verification: trims whitespace, strips any HTML/script
    /// tags (Razor also HTML-encodes on render, so this is defense-in-depth), and
    /// enforces a hard length ceiling regardless of what the client sent.
    /// </summary>
    private static string SanitizeContent(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.");

        var stripped = TagStripper.Replace(content, string.Empty).Trim();
        if (stripped.Length == 0)
            throw new ArgumentException("Content cannot be empty.");

        return stripped.Length > maxLength ? stripped[..maxLength] : stripped;
    }

    private static string NormalizePhone(string phone) =>
        new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());

    private Post Hydrate(Post p, string? currentUserId)
    {
        return new Post
        {
            Id = p.Id,
            AuthorId = p.AuthorId,
            Content = p.Content,
            Type = p.Type,
            ImageGradient = p.ImageGradient,
            CreatedOn = p.CreatedOn,
            LikedByUserIds = p.LikedByUserIds,
            Comments = p.Comments.Select(c => { c.Author = GetUserById(c.AuthorId); return c; }).ToList(),
            ShareCount = p.ShareCount,
            Author = GetUserById(p.AuthorId),
            LikedByCurrentUser = currentUserId != null && p.LikedByUserIds.Contains(currentUserId)
        };
    }

    private void Seed()
    {
        var maria = CreateUser("Maria Chen", "@maria.codes", "maria@avatisment.dev", "+15550100101", "seed");
        var leo = CreateUser("Leo Osei", "@leo.builds", "leo@avatisment.dev", "+15550100102", "seed");
        var nina = CreateUser("Nina Kapoor", "@nina.designs", "nina@avatisment.dev", "+15550100103", "seed");
        var sam = CreateUser("Sam Rivera", "@sam.travels", "sam@avatisment.dev", "+15550100104", "seed");

        foreach (var u in new[] { maria, leo, nina, sam })
        {
            u.EmailVerified = true;
            u.PhoneVerified = true;
        }

        maria.Bio = "Frontend engineer. Coffee-powered. Building pretty things ✨";
        leo.Bio = "Indie hacker · #buildinpublic · shipping every week 🚀";
        nina.Bio = "Product designer chasing good typography and better gradients 🎨";
        sam.Bio = "Travel photographer 📷 currently somewhere with good light";

        ToggleFollow(maria.Id, leo.Id);
        ToggleFollow(maria.Id, nina.Id);
        ToggleFollow(leo.Id, maria.Id);
        ToggleFollow(nina.Id, sam.Id);

        var p1 = CreatePost(nina.Id, "Redesigned our onboarding flow this week — cut drop-off by 18%. Small friction, big cost. #uxdesign #product", Gradients[2]);
        var p2 = CreatePost(leo.Id, "Shipped v2 of my side project today. New dashboard, dark mode, and finally a decent empty state. #buildinpublic", Gradients[1]);
        var p3 = CreatePost(maria.Id, "CSS grid + subgrid is criminally underused. Just replaced 40 lines of flexbox hacks with 6 lines. #frontend #css", null);
        var p4 = CreatePost(sam.Id, "Golden hour over the dunes. Sometimes you just wait two hours for ninety seconds of light. #travel #photography", Gradients[3], isReel: true);
        var p5 = CreatePost(nina.Id, "Hot take: your product doesn't need more features, it needs less friction. #productdesign", Gradients[4]);
        var p6 = CreatePost(leo.Id, "60 seconds of my build-in-public dashboard demo. #buildinpublic #reels", Gradients[0], isReel: true);

        ToggleLike(p1.Id, leo.Id);
        ToggleLike(p1.Id, maria.Id);
        ToggleLike(p1.Id, sam.Id);
        ToggleLike(p2.Id, nina.Id);
        ToggleLike(p3.Id, leo.Id);
        ToggleLike(p4.Id, maria.Id);
        ToggleLike(p4.Id, nina.Id);
        ToggleLike(p4.Id, leo.Id);
        ToggleLike(p6.Id, maria.Id);

        AddComment(p1.Id, leo.Id, "18%?! Okay you have to write this up, I need the details.");
        AddComment(p1.Id, sam.Id, "This is why design and eng need to talk more often 👏");
        AddComment(p4.Id, nina.Id, "The color grading on this is unreal.");
        AddComment(p2.Id, maria.Id, "Dark mode gang rise up 🌙");

        ToggleFollow(sam.Id, maria.Id);
        ToggleFollow(leo.Id, nina.Id);

        SendMessage(leo.Id, maria.Id, "Hey! Loved your grid vs flexbox post.");
        SendMessage(maria.Id, leo.Id, "Thank you! CSS subgrid changed my life honestly.");
        SendMessage(leo.Id, maria.Id, "Same. Want to pair on the dashboard redesign sometime?");
        SendMessage(nina.Id, maria.Id, "Quick q — did you ever finish that onboarding audit?");
    }
}
