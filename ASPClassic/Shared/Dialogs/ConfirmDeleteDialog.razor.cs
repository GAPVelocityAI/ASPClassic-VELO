using Microsoft.AspNetCore.Components;
using MudBlazor;
using ASPClassic.Shared.Dialogs;

namespace ASPClassic.Shared.Dialogs;

/// <summary>Generic confirmation dialog for delete operations.</summary>
public partial class ConfirmDeleteDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public string ContentText { get; set; } = "Are you sure you want to delete this item?";

    // Call sites were written against MudMessageBox's parameter names; accepting them here keeps
    // each site's own wording rather than replacing every call with one default.
    [Parameter] public string ButtonText { get; set; } = "Delete";
    [Parameter] public Color Color { get; set; } = MudBlazor.Color.Error;

    private void OnCancel()
    {
        MudDialog.Cancel();
    }

    private void OnConfirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
