namespace PKBank.Desktop.ViewModels;

/// <summary>An entry of the move selectors: display text, move id, and legality for the current entity.</summary>
public sealed record MoveChoice(string Text, int Value, bool IsIllegal);
