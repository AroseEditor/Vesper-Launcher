using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.Core.Diagnostics;

namespace Vesper.App.ViewModels;

public partial class ErrorsViewModel : ObservableObject
{
    private readonly ErrorService _service;

    [ObservableProperty]
    private bool _isOpen;

    public ErrorsViewModel(ErrorService? service = null)
    {
        _service = service ?? ErrorService.Shared;

        foreach (var error in _service.Snapshot())
            Errors.Add(error);

        _service.Reported += OnReported;
        _service.Cleared += OnCleared;
    }

    public static Func<string, Task>? ClipboardWriter { get; set; }

    public ObservableCollection<AppError> Errors { get; } = [];

    public int Count => Errors.Count;

    public bool HasErrors => Errors.Count > 0;

    public string BadgeText => Errors.Count > 9 ? "9+" : Errors.Count.ToString();

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private async Task CopyOne(AppError error)
    {
        if (ClipboardWriter is not null)
            await ClipboardWriter(error.ClipboardText);
    }

    [RelayCommand]
    private async Task CopyAll()
    {
        if (ClipboardWriter is not null)
            await ClipboardWriter(_service.CopyAll());
    }

    [RelayCommand]
    private void Clear() => _service.Clear();

    private void OnReported(object? sender, AppError error) => Dispatcher.UIThread.Post(() =>
    {
        Errors.Insert(0, error);
        IsOpen = true;
        RaiseCounts();
    });

    private void OnCleared(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Errors.Clear();
        IsOpen = false;
        RaiseCounts();
    });

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(BadgeText));
    }
}
