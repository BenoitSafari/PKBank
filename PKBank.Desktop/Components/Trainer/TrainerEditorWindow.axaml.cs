using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Trainer;

/// <summary>
/// Trainer editor mirroring WinForms' SAV_SimpleTrainer: name, gender, IDs,
/// money, coins/BP, play time and badges, applied to the save on demand.
/// </summary>
public sealed partial class TrainerEditorWindow : Window
{
    private readonly SaveFile? _sav;
    private readonly List<CheckBox> _badgeBoxes = [];
    private byte _gender;

    public TrainerEditorWindow() => InitializeComponent(); // designer

    public TrainerEditorWindow(SaveFile sav) : this()
    {
        _sav = sav;

        OTNameBox.MaxLength = sav.MaxStringLengthTrainer;
        OTNameBox.Text = sav.OT;

        _gender = sav.Gender;
        GenderButton.IsVisible = sav.Generation > 1;
        RefreshGenderButton();

        TidBox.Maximum = MaxDisplayTID(sav);
        TidBox.Value = sav.DisplayTID;
        SidBox.IsVisible = sav.Generation > 2;
        SidBox.Maximum = MaxDisplaySID(sav);
        SidBox.Value = sav.DisplaySID;

        MoneyBox.Maximum = sav.MaxMoney;
        MoneyBox.Value = sav.Money;

        // Gen 1/2 have game corner coins; Gen 5 reuses the row for Battle Subway BP.
        (bool visible, string label, decimal value) coins = sav switch
        {
            SAV1 s1 => (true, "Coins", s1.Coin),
            SAV2 s2 => (true, "Coins", s2.Coin),
            SAV5 s5 => (true, "BP", s5.BattleSubway.BP),
            _ => (false, "Coins", 0),
        };
        CoinsRow.IsVisible = coins.visible;
        CoinsLabel.Text = coins.label;
        CoinsBox.Maximum = sav.MaxCoins;
        CoinsBox.Value = coins.value;

        HoursBox.Value = sav.PlayedHours;
        MinutesBox.Value = sav.PlayedMinutes % 60;
        SecondsBox.Value = sav.PlayedSeconds % 60;

        LoadBadges(sav);
    }

    // Display values already account for the ID format: 16-bit pairs up to Gen 6,
    // 6-digit TID / 4-digit SID on Gen 7+.
    private static int MaxDisplayTID(SaveFile sav) => sav.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 999_999 : ushort.MaxValue;
    private static int MaxDisplaySID(SaveFile sav) => sav.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 4294 : ushort.MaxValue;

    private void LoadBadges(SaveFile sav)
    {
        var (count, value) = sav switch
        {
            SAV1 s1 => (8, s1.Badges),
            SAV2 s2 => (16, s2.Badges),
            SAV3 s3 => (8, s3.Badges),
            SAV4HGSS hgss => (16, hgss.Badges | (hgss.Badges16 << 8)),
            SAV4 s4 => (8, (int)s4.Badges),
            SAV5 s5 => (8, s5.Misc.Badges),
            _ => (0, 0), // Colosseum/XD and Gen 6+ have no simple badge bitfield
        };

        BadgesGroup.IsVisible = count > 0;
        for (var i = 0; i < count; i++)
        {
            var chk = new CheckBox
            {
                Content = (i + 1).ToString(),
                IsChecked = (value & (1 << i)) != 0,
            };
            _badgeBoxes.Add(chk);
            BadgePanel.Children.Add(chk);
        }
    }

    private void RefreshGenderButton() => GenderButton.Content = _gender == 1 ? "♀" : "♂";

    private void OnGenderClicked(object? sender, RoutedEventArgs e)
    {
        _gender = (byte)(_gender == 0 ? 1 : 0);
        RefreshGenderButton();
    }

    private void OnMaxMoneyClicked(object? sender, RoutedEventArgs e) => MoneyBox.Value = _sav?.MaxMoney ?? 0;

    private void OnMaxCoinsClicked(object? sender, RoutedEventArgs e) => CoinsBox.Value = _sav?.MaxCoins ?? 0;

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
        {
            Close();
            return;
        }

        var name = OTNameBox.Text ?? string.Empty;
        if (sav.OT != name) // only modify if changed (preserve trash bytes)
            sav.OT = name;
        if (sav.Generation > 1)
            sav.Gender = _gender;

        sav.DisplayTID = (uint)(TidBox.Value ?? 0);
        if (sav.Generation > 2)
            sav.DisplaySID = (uint)(SidBox.Value ?? 0);

        sav.Money = (uint)(MoneyBox.Value ?? 0);
        sav.PlayedHours = (int)(HoursBox.Value ?? 0);
        sav.PlayedMinutes = (int)(MinutesBox.Value ?? 0) % 60;
        sav.PlayedSeconds = (int)(SecondsBox.Value ?? 0) % 60;

        var badgeval = 0;
        for (var i = 0; i < _badgeBoxes.Count; i++)
            badgeval |= (_badgeBoxes[i].IsChecked == true ? 1 : 0) << i;

        var coins = (uint)(CoinsBox.Value ?? 0);
        switch (sav)
        {
            case SAV1 s1:
                s1.Coin = coins;
                s1.Badges = badgeval & 0xFF;
                break;
            case SAV2 s2:
                s2.Coin = coins;
                s2.Badges = badgeval & 0xFFFF;
                break;
            case SAV3 s3:
                s3.Badges = badgeval & 0xFF;
                break;
            case SAV4HGSS hgss:
                hgss.Badges = (byte)badgeval;
                hgss.Badges16 = badgeval >> 8;
                break;
            case SAV4 s4:
                s4.Badges = (byte)badgeval;
                break;
            case SAV5 s5:
                s5.BattleSubway.BP = (int)coins;
                s5.Misc.Badges = badgeval & 0xFF;
                break;
        }

        sav.State.Edited = true;
        Close();
    }
}
