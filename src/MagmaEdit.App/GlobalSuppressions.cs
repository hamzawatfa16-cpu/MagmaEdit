using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1859:Change type of field '_commandGateway' from 'MagmaEdit.Core.Editing.IEditorCommandGateway' to 'MagmaEdit.Core.Editing.EditorCommandGateway' for improved performance",
    Justification = "The interface type is intentional at the desktop boundary so the UI remains decoupled from the concrete command gateway implementation.",
    Scope = "field",
    Target = "~F:MagmaEdit.App.MainWindow._commandGateway")]
