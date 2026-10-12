using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Unayka.Core.Administration;
using Unayka.Core.Orders;
using Unayka.Web.Components.Base;

namespace Unayka.Web.Components.Admin;

public abstract class AdminPageBase : UnaykaComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    protected int? ActorUserId { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        var state = AuthenticationState is null ? null : await AuthenticationState;
        var claim = state?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        ActorUserId = int.TryParse(claim, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    protected static string FormatDateTime(DateTimeOffset value) => LocalDays.FormatDateTime(value);

    protected static string FormatDay(DateOnly day) => day.ToString("dd/MM", CultureInfo.InvariantCulture);

    protected static string ChannelLabel(OrderChannel channel) => channel == OrderChannel.Online ? "Online" : "Balcão";

    protected static string FormatNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
}
