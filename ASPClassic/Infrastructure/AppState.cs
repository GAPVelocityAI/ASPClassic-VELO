using System;
using System.Threading.Tasks;

namespace ASPClassic.Infrastructure;

/// <summary>
/// Global Blazor application state singleton.
/// <para>Legacy source: New abstraction — provides cross-component state and command dispatch
/// for layout-to-page communication in the Blazor Server shell.</para>
/// </summary>
public class AppState
{
    /// <summary>True when the signed-in user may see and edit administration screens.</summary>
    public bool IsAdmin { get; set; }
    public string CurrentUser { get; set; } = string.Empty;

    public string CurrentCompany { get; set; } = string.Empty;

    public int CurrentYear { get; set; } = DateTime.Now.Year;

    public bool IsLoggedIn { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public string ActiveDialogId { get; set; } = string.Empty;

    public bool IsBusy { get; set; }

    /// <summary>
    /// Event delegate invoked when a command is dispatched from the layout (toolbar/menu)
    /// to the active page. Pages subscribe with += and unsubscribe with -= in Dispose.
    /// </summary>
    public Func<string, Task>? OnCommand { get; set; }

    /// <summary>
    /// Event delegate invoked when any state property changes, so subscribers can re-render.
    /// </summary>
    public Func<Task>? OnChange { get; set; }

    /// <summary>
    /// Dispatches a named command from MainLayout to the currently active page component.
    /// The page subscribes to <see cref="OnCommand"/> to receive it.
    /// </summary>
    public async Task SendCommandAsync(string command)
    {
        if (OnCommand is not null)
        {
            await OnCommand.Invoke(command);
        }
    }

    /// <summary>
    /// Sets the <see cref="StatusMessage"/> and notifies subscribers via <see cref="OnChange"/>.
    /// </summary>
    public void LogStatus(string message)
    {
        StatusMessage = message;
        OnChange?.Invoke();
    }
}
