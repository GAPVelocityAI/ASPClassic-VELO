// ═══════════════════════════════════════════════════════════════
// Property of Growth Acceleration Partners
// Author: Jose Arroyo
// ═══════════════════════════════════════════════════════════════
//
// The editing surface behind RichTextEditor.razor.
//
// The legacy used Summernote, which brings jQuery and Bootstrap with it. Rather than pull three
// libraries in for one field, this is the same capability over a contenteditable element: the
// toolbar lives in Blazor and only the caret operations happen here, because a caret cannot be
// manipulated from C#.
//
// document.execCommand is formally deprecated and still the only thing every browser implements for
// this. Summernote used it too. If it is ever removed, the replacement is a Selection/Range
// implementation behind these same four functions — nothing above this file would change.

window.aspClassicRichText = {

    // Writes the stored HTML into the surface. Called on load and whenever the bound value changes
    // from outside, never on every keystroke — reassigning innerHTML moves the caret to the start,
    // which makes typing impossible.
    setHtml: function (element, html) {
        if (!element) return;
        if (element.innerHTML !== html) element.innerHTML = html || "";
    },

    getHtml: function (element) {
        return element ? element.innerHTML : "";
    },

    // Applies a formatting command to the current selection. The surface is focused first: a click
    // on a toolbar button moves focus out of the editable area, and a command with no selection in
    // scope silently does nothing.
    exec: function (element, command, value) {
        if (!element) return "";

        element.focus();

        try {
            document.execCommand(command, false, value ?? null);
        } catch (e) {
            console.warn("rich text command failed:", command, e);
        }

        return element.innerHTML;
    },

    // Pastes as plain text. Pasting from Word otherwise carries whole stylesheets into the value,
    // and what is stored here is rendered straight onto a page.
    attachPlainTextPaste: function (element) {
        if (!element || element.dataset.pasteBound === "true") return;

        element.addEventListener("paste", function (e) {
            e.preventDefault();
            const text = (e.clipboardData || window.clipboardData).getData("text/plain");
            document.execCommand("insertText", false, text);
        });

        element.dataset.pasteBound = "true";
    }
};
