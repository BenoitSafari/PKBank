using System;
using System.Collections;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Metadata;

namespace PKBank.Desktop.Controls;

/// <summary>
/// Drop-in replacement for a ComboBox that lets the user type to narrow a long
/// list (species, moves, items…) while still behaving like a picker: the list
/// opens on focus, matching ignores case and accents, and free text never
/// becomes a value.
/// <br/>
/// <see cref="AutoCompleteBox"/> only binds <see cref="AutoCompleteBox.SelectedItem"/>,
/// so <see cref="SelectedValue"/>, <see cref="SelectedValueBinding"/> and
/// <see cref="SelectedIndex"/> are reimplemented here to keep the existing
/// ComboBox bindings working unchanged.
/// </summary>
public class AutoCompleteSelect : AutoCompleteBox
{
    // Reuse the stock AutoCompleteBox template/theme.
    protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

    public static readonly StyledProperty<object?> SelectedValueProperty =
        AvaloniaProperty.Register<AutoCompleteSelect, object?>(nameof(SelectedValue), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<BindingBase?> SelectedValueBindingProperty =
        AvaloniaProperty.Register<AutoCompleteSelect, BindingBase?>(nameof(SelectedValueBinding));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<AutoCompleteSelect, int>(nameof(SelectedIndex), defaultValue: -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Value of the selected item, resolved through <see cref="SelectedValueBinding"/>.</summary>
    public object? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>Binding used to read <see cref="SelectedValue"/> off an item (e.g. ComboItem.Value).</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public BindingBase? SelectedValueBinding
    {
        get => GetValue(SelectedValueBindingProperty);
        set => SetValue(SelectedValueBindingProperty, value);
    }

    /// <summary>Index of the selected item within <see cref="AutoCompleteBox.ItemsSource"/>.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private bool _syncing;
    private TextBox? _editor;

    public AutoCompleteSelect()
    {
        FilterMode = AutoCompleteFilterMode.Custom;
        TextFilter = MatchesIgnoringCaseAndAccents;
        MinimumPrefixLength = 0;
        IsTextCompletionEnabled = false;
        ClearSelectionOnLostFocus = false; // free text must not wipe the value
    }

    /// <summary>Matches loosely so "chenip", "Chénip" and "CHENIP" all find "Chenipan".</summary>
    private static bool MatchesIgnoringCaseAndAccents(string? search, string? item)
    {
        if (string.IsNullOrEmpty(search))
            return true; // empty query: offer the whole list
        if (string.IsNullOrEmpty(item))
            return false;
        return Normalize(item).Contains(Normalize(search), StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_TextBox");
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        // Select the current text so typing replaces it, and show the list right
        // away so the control still reads as a picker.
        _editor?.SelectAll();
        IsDropDownOpen = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        IsDropDownOpen = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_syncing)
            return;

        if (change.Property == SelectedItemProperty)
            SyncFromItem();
        else if (change.Property == SelectedValueProperty)
            SyncFromValue();
        else if (change.Property == SelectedIndexProperty)
            SyncFromIndex();
        else if (change.Property == ItemsSourceProperty)
            SyncAfterItemsChanged();
    }

    /// <summary>The item drives the value and index.</summary>
    private void SyncFromItem()
    {
        // Rebuilding ItemsSource clears the selection; that is list churn, not the
        // user picking "nothing", so the bound value stays authoritative.
        if (SelectedItem is null)
            return;

        _syncing = true;
        try
        {
            SelectedValue = SelectedItem is null ? null : GetItemValue(SelectedItem);
            SelectedIndex = IndexOf(SelectedItem);
        }
        finally { _syncing = false; }
    }

    private void SyncFromValue()
    {
        var match = FindByValue(SelectedValue);
        if (match is null && SelectedValue is not null)
            return; // value not in the list (yet); keep what is displayed

        _syncing = true;
        try
        {
            SelectedItem = match;
            SelectedIndex = IndexOf(match);
        }
        finally { _syncing = false; }
    }

    private void SyncFromIndex()
    {
        var item = ItemAt(SelectedIndex);
        _syncing = true;
        try
        {
            SelectedItem = item;
            SelectedValue = item is null ? null : GetItemValue(item);
        }
        finally { _syncing = false; }
    }

    /// <summary>
    /// Rebuilding the list drops the selection; restore it from the value (or
    /// index) the view-model still holds instead of silently clearing the field.
    /// </summary>
    private void SyncAfterItemsChanged()
    {
        if (SelectedValue is not null)
            SyncFromValue();
        else if (SelectedIndex >= 0)
            SyncFromIndex();
    }

    private object? FindByValue(object? value)
    {
        if (value is null || ItemsSource is not IEnumerable items)
            return null;
        foreach (var item in items)
        {
            if (item is not null && Equals(GetItemValue(item), value))
                return item;
        }
        return null;
    }

    private int IndexOf(object? item)
    {
        if (item is null || ItemsSource is not IEnumerable items)
            return -1;
        int i = 0;
        foreach (var candidate in items)
        {
            if (Equals(candidate, item))
                return i;
            i++;
        }
        return -1;
    }

    private object? ItemAt(int index)
    {
        if (index < 0 || ItemsSource is not IEnumerable items)
            return null;
        int i = 0;
        foreach (var item in items)
        {
            if (i++ == index)
                return item;
        }
        return null;
    }

    /// <summary>Resolves an item's value the same way the drop-down resolves its labels.</summary>
    private object? GetItemValue(object item)
    {
        if (SelectedValueBinding is null)
            return item;

        var probe = new ValueProbe { DataContext = item };
        probe.Bind(ValueProbe.ValueProperty, SelectedValueBinding);
        var value = probe.Value;
        probe.DataContext = null;
        return value;
    }

    private sealed class ValueProbe : Control
    {
        public static readonly StyledProperty<object?> ValueProperty =
            AvaloniaProperty.Register<ValueProbe, object?>(nameof(Value));

        public object? Value => GetValue(ValueProperty);
    }
}
