namespace Avatisment.Models;

public class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;       // e.g. "@maria.codes"
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#7C5CFC";      // gradient seed for the avatar initials
    public string Bio { get; set; } = string.Empty;
    public string CoverGradient { get; set; } = "linear-gradient(135deg,#7C5CFC,#22D3EE)";
    public DateTime JoinedOn { get; set; } = DateTime.UtcNow;
    public List<string> FollowerIds { get; set; } = new();
    public List<string> FollowingIds { get; set; } = new();

    // ---- Verification state ----
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public string? EmailVerificationCode { get; set; }
    public string? PhoneVerificationCode { get; set; }
    public DateTime? EmailCodeExpiresOn { get; set; }
    public DateTime? PhoneCodeExpiresOn { get; set; }

    // ---- Login security ----
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedOutUntil { get; set; }

    public string Initials =>
        string.Join("", DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpper(w[0])));
}
