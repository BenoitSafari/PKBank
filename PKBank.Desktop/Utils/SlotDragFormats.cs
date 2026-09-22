using System.Collections.Generic;
using Avalonia.Input;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Utils;

/// <summary>In-process drag formats shared by the window and the editor drop targets.</summary>
public static class SlotDragFormats
{
    /// <summary>Single-slot drag: move/swap semantics inside the app.</summary>
    public static readonly DataFormat<SlotViewModel> Slot =
        DataFormat.CreateInProcessFormat<SlotViewModel>("pkhex-avalonia-slot");

    /// <summary>Multi-selection drag, in display order: fills the free slots from the drop target on.</summary>
    public static readonly DataFormat<IReadOnlyList<SlotViewModel>> Multi =
        DataFormat.CreateInProcessFormat<IReadOnlyList<SlotViewModel>>("pkhex-avalonia-multi");
}
