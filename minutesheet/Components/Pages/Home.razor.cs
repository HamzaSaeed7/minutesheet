using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using minutesheet.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace minutesheet.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] protected ApplicationDbContext DbContext { get; set; } = default!;
    [Inject] protected UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] protected AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    private string _firstName = "there";
    private string? _avatarPath;
    private bool _isDark;
    private bool _showNotifications;
    private int _pendingApproval;
    private int _reviewsToResolve;
    private int _approved;
    private int _total;
    private List<MinuteSheet> _recent = new();
    private List<ApprovalStep> _pendingSteps = new();
    private List<MinuteSheet> _returnedSheets = new();
    private List<(string Label, int Count)> _monthly = new();
    private List<(string Label, int Count)> _monthlyApproved = new();
    private Dictionary<string, string> _approverNames = new();

    private string Greeting => DateTime.Now.Hour < 12 ? "Good morning" : DateTime.Now.Hour < 18 ? "Good afternoon" : "Good evening";

    // ---- Smooth trend chart helpers ----

    private List<(double X, double Y)> TrendPoints(double[] values, double width, double height, double pad = 8)
    {
        var list = new List<(double X, double Y)>();
        if (values.Length == 0) return list;
        var max = values.Max();
        if (max <= 0) max = 1;
        var n = values.Length;
        for (var i = 0; i < n; i++)
        {
            var x = n == 1 ? width / 2 : pad + i * (width - 2 * pad) / (n - 1);
            var y = height - pad - (values[i] / max) * (height - 2 * pad);
            list.Add((x, y));
        }
        return list;
    }

    private string SmoothPath(double[] values, double width, double height, double pad = 8)
    {
        if (values.Length == 0) return "";
        var pts = TrendPoints(values, width, height, pad);
        if (pts.Count == 1)
            return $"M{F(pts[0].X)} {F(pts[0].Y)}";
        var sb = new StringBuilder();
        sb.Append("M").Append(F(pts[0].X)).Append(' ').Append(F(pts[0].Y));
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[Math.Max(i - 1, 0)];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = pts[Math.Min(i + 2, pts.Count - 1)];
            var c1x = p1.X + (p2.X - p0.X) / 6;
            var c1y = p1.Y + (p2.Y - p0.Y) / 6;
            var c2x = p2.X - (p3.X - p1.X) / 6;
            var c2y = p2.Y - (p3.Y - p1.Y) / 6;
            sb.Append(" C").Append(F(c1x)).Append(' ').Append(F(c1y))
              .Append(", ").Append(F(c2x)).Append(' ').Append(F(c2y))
              .Append(", ").Append(F(p2.X)).Append(' ').Append(F(p2.Y));
        }
        return sb.ToString();
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private string SmoothAreaFill(double[] values, double width, double height, double pad = 8)
    {
        if (values.Length == 0) return "";
        var pts = TrendPoints(values, width, height, pad);
        var line = SmoothPath(values, width, height, pad);
        return $"{line} L{F(pts[^1].X)} {F(height)} L{F(pts[0].X)} {F(height)} Z";
        string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ---- Avatar helpers ----

    private static string AvatarInitials(string name)
    {
        var parts = (name ?? "?").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1)
        {
            var w = parts[0];
            return (w.Length >= 2 ? w[..2] : w).ToUpperInvariant();
        }
        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    private static string AvatarColor(string name)
    {
        var h = Math.Abs((name ?? "?").GetHashCode());
        var r = 60 + h % 140;
        var g = 60 + (h >> 8) % 140;
        var b = 60 + (h >> 16) % 140;
        return $"rgb({r},{g},{b})";
    }

    // -------------------------------

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = await UserManager.GetUserAsync(authState.User);
        if (user is null)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? "there") : user.FullName;
        _firstName = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
        _avatarPath = user.AvatarPath;
        var email = user.Email ?? "";

        // Load all non-approved steps where this user is the approver,
        // including the sheet's full step list so we can check IsActionable.
        var mySteps = await DbContext.ApprovalSteps
            .Where(s => s.ApproverEmail == email && s.Status != ApprovalStepStatus.Approved)
            .Include(s => s.MinuteSheet)
                .ThenInclude(m => m!.ApprovalSteps)
            .OrderByDescending(s => s.MinuteSheet!.CreatedAt)
            .ToListAsync();

        // Only count / show steps where it is actually this user's turn
        // (sequential approval: lower-indexed pending steps must go first).
        var actionableSteps = mySteps
            .Where(s => ApprovalWorkflow.IsActionable(s, s.MinuteSheet!.ApprovalSteps))
            .ToList();

        _pendingApproval = actionableSteps.Count(s => s.Status == ApprovalStepStatus.Pending);

        _pendingSteps = actionableSteps
            .Where(s => s.Status == ApprovalStepStatus.Pending)
            .OrderByDescending(s => s.MinuteSheet!.CreatedAt)
            .Take(6)
            .ToList();

        var mySheets = await DbContext.MinuteSheets
            .Where(m => m.CreatedByUserId == user.Id)
            .Include(m => m.ApprovalSteps)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        _total = mySheets.Count;
        _approved = mySheets.Count(m => ApprovalWorkflow.IsFullyApproved(m.ApprovalSteps));
        // Sheets the user created that have been sent back for review (matching Actions.razor logic).
        _reviewsToResolve = mySheets.Count(m => m.ApprovalSteps.Any(s => s.Status == ApprovalStepStatus.Reviewed));
        _returnedSheets = mySheets
            .Where(m => m.ApprovalSteps.Any(s => s.Status == ApprovalStepStatus.Reviewed))
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .ToList();
        _recent = mySheets.Take(5).ToList();

        var approverEmails = _recent
            .SelectMany(m => m.ApprovalSteps)
            .Where(s => s.Status == ApprovalStepStatus.Approved || s.Status == ApprovalStepStatus.Reviewed)
            .Select(s => s.ApproverEmail)
            .Distinct()
            .ToList();

        var approverUsers = await DbContext.Users
            .Where(u => approverEmails.Contains(u.Email ?? ""))
            .ToListAsync();
        _approverNames = approverUsers
            .Where(u => u.Email != null)
            .ToDictionary(
                u => u.Email!,
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email! : u.FullName);

        var firstOfThisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        for (var i = 5; i >= 0; i--)
        {
            var month = firstOfThisMonth.AddMonths(-i);
            var count = mySheets.Count(m =>
            {
                var d = m.CreatedAt.ToLocalTime();
                return d.Year == month.Year && d.Month == month.Month;
            });
            var approvedCount = mySheets.Count(m =>
            {
                var d = m.CreatedAt.ToLocalTime();
                return d.Year == month.Year && d.Month == month.Month && ApprovalWorkflow.IsFullyApproved(m.ApprovalSteps);
            });
            _monthly.Add((month.ToString("MMM"), count));
            _monthlyApproved.Add((month.ToString("MMM"), approvedCount));
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var saved = await JS.InvokeAsync<string?>("localStorage.getItem", "ms-theme");
                _isDark = saved == "dark";
                StateHasChanged();
            }
            catch
            {
                // localStorage not available (e.g. prerender) — default to light.
            }
        }
    }

    private async Task ToggleTheme()
    {
        _isDark = !_isDark;
        var value = _isDark ? "dark" : "light";
        try
        {
            // Persist in both localStorage (client fallback) and a cookie (so the
            // server pre-renders every page with the correct theme — this is what
            // keeps dark mode applied across navigation without a reload).
            await JS.InvokeVoidAsync("eval",
                $"localStorage.setItem('ms-theme','{value}'); document.cookie = 'ms-theme={value}; path=/; max-age=31536000; SameSite=Lax'; document.body.dataset.theme = '{value}';");
        }
        catch
        {
            // ignore if storage unavailable
        }
    }

    private void ToggleNotifications()
    {
        _showNotifications = !_showNotifications;
    }

    private void GoToSheet(ApprovalStep step)
    {
        _showNotifications = false;
        Nav.NavigateTo($"dashboard/sheet/{step.MinuteSheet.Token}");
    }

    private void GoToReturnedSheet(Guid token)
    {
        _showNotifications = false;
        Nav.NavigateTo($"dashboard/sheet/{token}");
    }
}
