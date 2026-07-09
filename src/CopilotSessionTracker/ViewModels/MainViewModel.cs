using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopilotSessionTracker.Models;
using CopilotSessionTracker.Services;

namespace CopilotSessionTracker.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SessionStore _store = new();
    private IReadOnlyList<SessionInfo> _all = Array.Empty<SessionInfo>();

    [ObservableProperty]
    public partial ObservableCollection<SessionInfo> Sessions { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    public MainViewModel()
    {
        Sessions = new ObservableCollection<SessionInfo>();
        SearchText = string.Empty;

        // Give a helpful message immediately if the store is missing.
        StatusText = _store.DatabaseExists
            ? "Loading…"
            : $"session-store.db not found under {_store.SessionStateDir}";
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusText = "Loading sessions…";
        try
        {
            _all = await Task.Run(() => _store.LoadSessions());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _all = Array.Empty<SessionInfo>();
            Sessions = new ObservableCollection<SessionInfo>();
            StatusText = $"Failed to load sessions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public IReadOnlyList<ConversationTurn> GetRecentTurns(SessionInfo session, int count = 5) =>
        _store.LoadRecentTurns(session.Id, count);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        IEnumerable<SessionInfo> filtered = _all;
        if (query.Length > 0)
        {
            filtered = _all.Where(s =>
                Contains(s.Name, query) ||
                Contains(s.Id, query) ||
                Contains(s.WorkingDirectory, query) ||
                Contains(s.Repository, query));
        }

        Sessions = new ObservableCollection<SessionInfo>(filtered);
        StatusText = _all.Count == 0
            ? "No local sessions found."
            : $"{Sessions.Count} of {_all.Count} local session(s)";
    }

    private static bool Contains(string? source, string term) =>
        source is not null && source.Contains(term, StringComparison.OrdinalIgnoreCase);
}
