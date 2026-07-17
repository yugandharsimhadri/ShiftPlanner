using System.Windows.Input;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One row in the Requests tab's three sections (pending approval, open swaps,
/// my requests) — deliberately generic (Leave and Swap rows share this shape) so the page
/// can render every section with the same BindableLayout item template.</summary>
public sealed class RequestRowViewModel
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public Color StatusColor { get; init; } = Colors.Transparent;
    public Color StatusBackground { get; init; } = Colors.Transparent;

    public string? PrimaryActionText { get; init; }
    public ICommand? PrimaryActionCommand { get; init; }
    public string? SecondaryActionText { get; init; }
    public ICommand? SecondaryActionCommand { get; init; }

    public bool HasPrimaryAction => PrimaryActionCommand is not null;
    public bool HasSecondaryAction => SecondaryActionCommand is not null;
}
