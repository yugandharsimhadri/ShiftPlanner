using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShiftPlanner.Desktop.Helpers;
using ShiftPlanner.Desktop.Models;

namespace ShiftPlanner.Desktop.ViewModels;

public partial class DayCellViewModel : ObservableObject
{
    private readonly Action<DateOnly, string?> onAssign;
    private readonly Dictionary<string, ShiftType> shiftLookup;

    public DateOnly Date { get; }
    public IReadOnlyList<ShiftType> AvailableShiftTypes { get; }

    [ObservableProperty] private string? shiftCode;
    [ObservableProperty] private bool isPopupOpen;
    [ObservableProperty] private bool isFlagged;
    [ObservableProperty] private string? flagReason;

    public DayCellViewModel(DateOnly date, string? shiftCode, IReadOnlyList<ShiftType> shiftTypes, Action<DateOnly, string?> onAssign)
    {
        Date = date;
        this.shiftCode = shiftCode;
        AvailableShiftTypes = shiftTypes;
        shiftLookup = shiftTypes.ToDictionary(s => s.Code);
        this.onAssign = onAssign;
    }

    public string Label => string.IsNullOrEmpty(ShiftCode) ? "·" : ShiftCode;

    public Brush Background => ShiftCode is not null && shiftLookup.TryGetValue(ShiftCode, out var s)
        ? ColorHelper.Brush(Tint(s.Color, 0.85))
        : ColorHelper.Brush("#EEF1F0");

    public Brush Foreground => ShiftCode is not null && shiftLookup.TryGetValue(ShiftCode, out var s)
        ? ColorHelper.Brush(s.Color)
        : ColorHelper.Brush("#7C8D89");

    [RelayCommand]
    private void Assign(string? code)
    {
        ShiftCode = code;
        IsPopupOpen = false;
        onAssign(Date, code);
    }

    partial void OnShiftCodeChanged(string? value)
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(Foreground));
    }

    private static string Tint(string hex, double towardsWhite)
    {
        var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex)!;
        byte Mix(byte channel) => (byte)(channel + (255 - channel) * towardsWhite);
        return $"#{Mix(c.R):X2}{Mix(c.G):X2}{Mix(c.B):X2}";
    }
}
