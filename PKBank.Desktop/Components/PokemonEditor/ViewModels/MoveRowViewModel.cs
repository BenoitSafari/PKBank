using System.Collections.Generic;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.PokemonEditor.ViewModels;

/// <summary>
///     One move slot (selector, PP Ups, computed PP) of the Pokémon editor.
///     <see cref="Index" /> is the PKM move index (0-3).
/// </summary>
public sealed class MoveRowViewModel(PokemonEditorViewModel owner, int index) : ViewModelBase
{
    public int Index { get; } = index;

    /// <summary>
    ///     Shared choice list, re-exposed per row so the template binds without reaching
    ///     back to the parent's DataContext.
    /// </summary>
    public IReadOnlyList<MoveChoice> MoveList => owner.MoveList;

    public int Move
    {
        get => owner.Entity.GetMove(Index);
        set => owner.SetMove(Index, value);
    }

    // Nullable so an emptied field maps to 0.
    public int? PPUps
    {
        get => owner.GetPPUps(Index);
        set => owner.SetPPUps(Index, value);
    }

    public string PPDisplay => owner.GetPPDisplay(Index);

    /// <summary>Hover summary for the selector (null when the slot has no move).</summary>
    public MoveTipViewModel? Tip => MoveTipViewModel.TryCreate(owner.Entity, Index, owner.GetPPUps(Index));

    /// <summary>Values derived from the slot; safe to call while an input holds focus.</summary>
    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(PPDisplay));
        OnPropertyChanged(nameof(Tip));
    }

    public void RefreshMove()
    {
        OnPropertyChanged(nameof(Move));
        RefreshComputed();
    }

    public void RefreshPPUps()
    {
        OnPropertyChanged(nameof(PPUps));
        RefreshComputed();
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Move));
        OnPropertyChanged(nameof(PPUps));
        RefreshComputed();
    }

    /// <summary>The choice list was rebuilt; the selector needs both the items and the selection back.</summary>
    public void RefreshMoveList() => OnPropertyChanged(nameof(MoveList));

    public void RefreshSelection() => OnPropertyChanged(nameof(Move));
}
