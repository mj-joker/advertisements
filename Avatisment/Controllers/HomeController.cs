using System.Security.Claims;
using Avatisment.Models;
using Avatisment.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Avatisment.Controllers;

/// <summary>
/// Every action here requires a signed-in AND fully verified (email + phone) account.
/// Unverified users are bounced to the relevant verification step before they can
/// see or do anything on the site.
/// </summary>
[Authorize]
[ServiceFilter(typeof(RequireVerifiedAccountFilter))]
public class HomeController : Controller
{
    private readonly IAvatismentDataStore _store;
    private static readonly string[] Gradients =
    {
        "linear-gradient(135deg,#7C5CFC,#22D3EE)",
        "linear-gradient(135deg,#FF6B9D,#FFA36B)",
        "linear-gradient(135deg,#22D3EE,#3AF0B7)",
        null!, null!, null! // bias toward text-only posts
    };

    public HomeController(IAvatismentDataStore store)
    {
        _store = store;
    }

    private string? CurrentUserId =>
        User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public IActionResult Index()
    {
        var uid = CurrentUserId!;
        SetUnreadBadge(uid);
        var vm = new FeedViewModel
        {
            CurrentUser = _store.GetUserById(uid),
            Posts = _store.GetFeed(uid).ToList(),
            SuggestedUsers = _store.SuggestedUsersFor(uid).ToList(),
            TrendingTags = _store.TrendingTags().ToList()
        };
        return View(vm);
    }

    public IActionResult Profile(string handle)
    {
        var uid = CurrentUserId!;
        SetUnreadBadge(uid);
        var profileUser = _store.GetUserByHandle(handle) ?? _store.GetUserById(uid);
        if (profileUser is null) return NotFound();

        var vm = new ProfileViewModel
        {
            CurrentUser = _store.GetUserById(uid),
            ProfileUser = profileUser,
            Posts = _store.GetPostsByUser(profileUser.Id, uid).ToList(),
            IsFollowing = _store.GetUserById(uid)?.FollowingIds.Contains(profileUser.Id) ?? false,
            IsOwnProfile = profileUser.Id == uid
        };
        return View(vm);
    }

    public IActionResult Explore(string? q)
    {
        var uid = CurrentUserId!;
        SetUnreadBadge(uid);
        var vm = new ExploreViewModel
        {
            CurrentUser = _store.GetUserById(uid),
            Query = q,
            TrendingTags = _store.TrendingTags().ToList()
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            vm.MatchingUsers = _store.SearchUsers(q).Where(u => u.Id != uid).ToList();
            vm.MatchingPosts = _store.SearchPosts(q, uid).ToList();
        }
        else
        {
            vm.TrendingPosts = _store.TrendingPosts(uid).ToList();
            vm.DiscoverPeople = _store.SuggestedUsersFor(uid, 8).ToList();
        }

        return View(vm);
    }

    public IActionResult Notifications()
    {
        var uid = CurrentUserId!;
        var vm = new NotificationsViewModel
        {
            CurrentUser = _store.GetUserById(uid),
            Notifications = _store.GetNotifications(uid).ToList()
        };
        _store.MarkAllNotificationsRead(uid);
        ViewBag.UnreadCount = 0;
        ViewBag.CurrentUser = vm.CurrentUser;
        return View(vm);
    }

    public IActionResult Messages(string? handle)
    {
        var uid = CurrentUserId!;
        SetUnreadBadge(uid);
        var partner = string.IsNullOrEmpty(handle) ? null : _store.GetUserByHandle(handle);

        var vm = new MessagesViewModel
        {
            CurrentUser = _store.GetUserById(uid),
            Conversations = _store.GetConversations(uid)
                .OrderByDescending(c => c.LastMessage?.CreatedOn ?? DateTime.MinValue)
                .ToList(),
            ActivePartner = partner,
            Thread = partner != null ? _store.GetThread(uid, partner.Id).ToList() : new List<ChatMessage>()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SendMessage(string toUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(toUserId))
            return Json(new { ok = false });

        try
        {
            var msg = _store.SendMessage(CurrentUserId!, toUserId, content);
            return Json(new { ok = true, content = msg.Content, createdOn = msg.CreatedOn.ToString("h:mm tt") });
        }
        catch (ArgumentException)
        {
            return Json(new { ok = false, error = "Message couldn't be verified." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult CreatePost(CreatePostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            TempData["ComposeError"] = "Post can't be empty.";
            return RedirectToAction("Index");
        }

        try
        {
            var gradient = Gradients[Random.Shared.Next(Gradients.Length)];
            _store.CreatePost(CurrentUserId!, request.Content, gradient, request.IsReel);
        }
        catch (ArgumentException)
        {
            TempData["ComposeError"] = "That post couldn't be verified — please remove any markup and try again.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ToggleLike(string postId)
    {
        _store.ToggleLike(postId, CurrentUserId!);
        var post = _store.GetFeed(CurrentUserId).FirstOrDefault(p => p.Id == postId);
        return Json(new { liked = post?.LikedByCurrentUser ?? false, count = post?.LikedByUserIds.Count ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult AddComment(CommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Json(new { ok = false });

        try
        {
            var comment = _store.AddComment(request.PostId, CurrentUserId!, request.Content);
            return Json(new
            {
                ok = true,
                author = comment.Author?.DisplayName,
                initials = comment.Author?.Initials,
                avatarColor = comment.Author?.AvatarColor,
                content = comment.Content
            });
        }
        catch (ArgumentException)
        {
            return Json(new { ok = false, error = "Comment couldn't be verified." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ToggleFollow(string targetUserId)
    {
        _store.ToggleFollow(CurrentUserId!, targetUserId);
        var isFollowing = _store.GetUserById(CurrentUserId!)?.FollowingIds.Contains(targetUserId) ?? false;
        return Json(new { following = isFollowing });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();

    private void SetUnreadBadge(string uid)
    {
        ViewBag.UnreadCount = _store.GetNotifications(uid).Count(n => !n.Read);
        ViewBag.CurrentUser = _store.GetUserById(uid);
    }
}

/// <summary>
/// Blocks access to the app for signed-in accounts that haven't finished email
/// and phone verification yet, redirecting them back into the verification flow.
/// </summary>
public class RequireVerifiedAccountFilter : IAsyncActionFilter
{
    private readonly IAvatismentDataStore _store;

    public RequireVerifiedAccountFilter(IAvatismentDataStore store)
    {
        _store = store;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var account = uid != null ? _store.GetUserById(uid) : null;

            if (account != null)
            {
                if (!account.EmailVerified)
                {
                    context.Result = new RedirectToActionResult("VerifyEmail", "Account", null);
                    return;
                }
                if (!account.PhoneVerified)
                {
                    context.Result = new RedirectToActionResult("VerifyPhone", "Account", null);
                    return;
                }
            }
        }

        await next();
    }
}
