// ═══════════════════════════════════════════════════════════════
// Property of Growth Acceleration Partners
// Author: Jose Arroyo
// ═══════════════════════════════════════════════════════════════
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace ASPClassic.Shared;

/// <summary>
/// A rich-text field, replacing the Summernote editor the legacy used.
/// </summary>
/// <remarks>
/// <para>Summernote requires jQuery and Bootstrap. Rather than bring three libraries in for one
/// field, the toolbar is MudBlazor and only the caret operations are JavaScript — a selection cannot
/// be manipulated from C#.</para>
/// <para>The value is HTML, as it was in the legacy: it is written into a page below the view's
/// title, so it has always been markup rather than text.</para>
/// </remarks>
public partial class RichTextEditor
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    /// <summary>The HTML being edited.</summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Height of the editing surface. The legacy box was about six lines.</summary>
    [Parameter] public string MinHeight { get; set; } = "160px";

    private ElementReference _surface;
    private bool _sourceMode;

    /// <summary>
    /// The last value this component pushed into the surface.
    /// </summary>
    /// <remarks>
    /// Writing to <c>innerHTML</c> moves the caret to the start, so it must happen only when the
    /// value changed from OUTSIDE — never in response to typing, which would make the field
    /// impossible to type in.
    /// </remarks>
    private string _pushed = string.Empty;

    private static readonly string[] Fonts =
    {
        "Source Sans Pro", "Arial", "Georgia", "Tahoma", "Times New Roman", "Courier New", "Verdana",
    };

    // execCommand's fontSize takes 1-7, not points; the labels say what those mean.
    private static readonly (string Value, string Label)[] Sizes =
    {
        ("1", "Smallest"), ("2", "Small"), ("3", "Normal"),
        ("4", "Medium"), ("5", "Large"), ("6", "Larger"), ("7", "Largest"),
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_sourceMode) return;

        if (firstRender)
            await JS.InvokeVoidAsync("aspClassicRichText.attachPlainTextPaste", _surface);

        // Only when it changed elsewhere — see _pushed.
        if (firstRender || Value != _pushed)
        {
            _pushed = Value ?? string.Empty;
            await JS.InvokeVoidAsync("aspClassicRichText.setHtml", _surface, _pushed);
        }
    }

    private async Task OnSurfaceInput(ChangeEventArgs _)
    {
        var html = await JS.InvokeAsync<string>("aspClassicRichText.getHtml", _surface);

        // Recorded as already pushed, so the render that follows does not write it back and reset
        // the caret to the start of the field.
        _pushed = html;
        await SetValueAsync(html);
    }

    private async Task ExecAsync(string command, string? value = null)
    {
        var html = await JS.InvokeAsync<string>("aspClassicRichText.exec", _surface, command, value);

        _pushed = html;
        await SetValueAsync(html);
    }

    private async Task OnLinkAsync()
    {
        var result = await DialogService.ShowMessageBox(
            "Insert link", "Select the text to link first, then enter the address.",
            yesText: "Insert", cancelText: "Cancel");

        if (result != true) return;

        // execCommand takes the URL directly; a prompt is the smallest thing that asks for one.
        var url = await JS.InvokeAsync<string?>("prompt", "Address:", "https://");

        if (!string.IsNullOrWhiteSpace(url)) await ExecAsync("createLink", url);
    }

    private async Task ToggleSourceAsync()
    {
        _sourceMode = !_sourceMode;

        // Coming back from source view the surface is a new element, so the value has to be pushed
        // into it again.
        if (!_sourceMode) _pushed = string.Empty;

        await Task.CompletedTask;
    }

    private async Task OnSourceChanged(string html) => await SetValueAsync(html);

    private async Task SetValueAsync(string html)
    {
        Value = html;
        await ValueChanged.InvokeAsync(html);
    }
}
