using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Unayka.Web.Components.Base;

namespace Unayka.Web.Components.Counter;

public abstract class CounterPageBase : UnaykaComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    protected int UserId { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        var state = AuthenticationState is null ? null : await AuthenticationState;
        var claim = state?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        UserId = int.TryParse(claim, out var id) ? id : 0;
    }
}
