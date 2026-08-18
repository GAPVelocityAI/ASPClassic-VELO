using Microsoft.AspNetCore.Components;
using ASPClassic.Infrastructure;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>inc_nav_top.asp</c> (Inc_Nav_Top) — top navigation bar component.</summary>
public partial class IncNavTopComponent : ComponentBase
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    /// <summary>
    /// Controls whether the full navbar (messages, notifications, search, home, contact) is shown.
    /// Legacy code had <c>IF False THEN</c> — the full navbar was disabled by default.
    /// The condition was hardcoded to False, meaning the ELSE branch (minimal navbar) always ran.
    /// Set to true to enable the extended navbar features.
    /// </summary>
    [Parameter] public bool ShowFullNavbar { get; set; } = false;

    /// <summary>Fired when the sidebar toggle (hamburger) button is clicked.</summary>
    [Parameter] public EventCallback OnToggleSidebar { get; set; }

    /// <summary>Fired when the administration/settings button is clicked.</summary>
    [Parameter] public EventCallback OnAdministration { get; set; }

    /// <summary>Number of notifications to display on the badge. Legacy hardcoded 15.</summary>
    [Parameter] public int NotificationCount { get; set; } = 15;

    /// <summary>
    /// Port of the legacy <c>inc_nav_top.asp</c> initialization logic.
    /// The legacy code evaluated <c>IF False THEN</c> which always resulted in the ELSE branch
    /// being executed, rendering only the minimal navbar with a sidebar toggle button and an
    /// administration/settings button. The full navbar (home link, contact link, messages dropdown
    /// with 3 hardcoded messages from Brad Diesel / John Pierce / Nora Silvester, notifications
    /// dropdown with 15 notifications, and a search form) was dead code that never rendered.
    ///
    /// In the Blazor port, this behavior is controlled declaratively by the <see cref="ShowFullNavbar"/>
    /// parameter (default: false = minimal navbar). When set to true, all the legacy full-navbar
    /// elements are rendered. This method applies that logic by ensuring the component state is
    /// consistent with the parameter value.
    /// </summary>
    public void LoadIncNavTop()
    {
        // The legacy ASP code structure was:
        //   IF False THEN
        //     (render full navbar with home, contact, search, messages dropdown, notifications dropdown, admin button)
        //   ELSE
        //     (render minimal navbar with only sidebar toggle and admin button)
        //   END IF
        //
        // Since the condition was literally "False", the full navbar never rendered.
        // In Blazor, this is handled declaratively in the .razor template via @if (ShowFullNavbar).
        // The ShowFullNavbar parameter defaults to false, preserving the legacy behavior.
        //
        // If a caller sets ShowFullNavbar = true, the component renders all elements that were
        // in the dead IF branch: Home link (navigates to /aspclassic-vbscript/default),
        // Contact link, Messages dropdown (3 messages), Notifications dropdown (15 notifications),
        // and the Administration settings button.
        //
        // No additional runtime state mutation is needed — Blazor's declarative rendering
        // handles the conditional display based on the parameter value.
        StateHasChanged();
    }

    private async Task OnToggleSidebarClick()
    {
        if (OnToggleSidebar.HasDelegate)
        {
            await OnToggleSidebar.InvokeAsync();
        }
        else
        {
            // Legacy: <a class="nav-link" data-widget="pushmenu" href="#"> toggled the sidebar
            await AppState.SendCommandAsync("toggle-sidebar");
        }
    }

    private async Task OnHomeClick()
    {
        // Legacy: <a href="<%= SITE_ROOT %>default.asp" class="nav-link">Home</a>
        NavigationManager.NavigateTo("/aspclassic-vbscript/default");
        await Task.CompletedTask;
    }

    private async Task OnContactClick()
    {
        // Legacy: <a href="#" class="nav-link">Contact</a> — placeholder link in the original
        await AppState.SendCommandAsync("contact");
    }

    private async Task OnMessageClick(string senderName)
    {
        // Legacy: clicking a message dropdown item. The original had three hardcoded messages:
        //   Brad Diesel — "Call me whenever you can..." (4 Hours Ago)
        //   John Pierce — "I got your message bro" (4 Hours Ago)
        //   Nora Silvester — "The subject goes here" (4 Hours Ago)
        // All linked to "#". In the modern app, dispatch a command with sender context.
        await AppState.SendCommandAsync($"view-message:{senderName}");
    }

    private async Task OnSeeAllMessagesClick()
    {
        // Legacy: <a href="#" class="dropdown-item dropdown-footer">See All Messages</a>
        await AppState.SendCommandAsync("see-all-messages");
    }

    private async Task OnNotificationClick(string notificationType)
    {
        // Legacy: clicking a notification item. Original had three notification types:
        //   4 new messages (envelope icon, 3 mins ago)
        //   8 friend requests (users icon, 12 hours ago)
        //   3 new reports (file icon, 2 days ago)
        // All linked to "#". Dispatch command with notification type.
        await AppState.SendCommandAsync($"view-notification:{notificationType}");
    }

    private async Task OnSeeAllNotificationsClick()
    {
        // Legacy: <a href="#" class="dropdown-item dropdown-footer">See All Notifications</a>
        await AppState.SendCommandAsync("see-all-notifications");
    }

    private async Task OnAdministrationClick()
    {
        if (OnAdministration.HasDelegate)
        {
            await OnAdministration.InvokeAsync();
        }
        else
        {
            // Legacy: <a data-widget="control-sidebar" data-slide="true" href="#">
            //   <i class="fas fa-cogs"></i>
            // </a>
            // This toggled the AdminLTE control sidebar (right-side admin panel).
            await AppState.SendCommandAsync("toggle-admin-sidebar");
        }
    }
}
