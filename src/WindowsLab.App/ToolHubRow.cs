using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WindowsLab.Core;

namespace WindowsLab.App;

public sealed class ToolHubRow : INotifyPropertyChanged
{
    private string _statusText = "";
    private string _actionText = "";
    private string _docsButtonText = Loc.T("tools.hub.docs");
    private bool _actionEnabled = true;
    private bool _isPresent;

    public ToolHubRow(ToolHubEntry entry)
    {
        Entry = entry;
        PrimaryCommand = new RelayCommand(() => PrimaryRequested?.Invoke(this));
        DocsCommand = new RelayCommand(() => DocsRequested?.Invoke(this));
    }

    public ToolHubEntry Entry { get; }

    public string Title => Entry.Title;

    public string Summary => Loc.IsEnglish ? Entry.SummaryEn : Entry.Summary;

    public string Improves => Loc.IsEnglish ? Entry.ImprovesEn : Entry.Improves;

    public string RiskNote
    {
        get
        {
            var note = Loc.IsEnglish ? Entry.RiskNoteEn : Entry.RiskNote;
            return string.IsNullOrWhiteSpace(note) ? "" : note;
        }
    }

    public bool HasRiskNote => !string.IsNullOrWhiteSpace(RiskNote);

    public bool HasDocs => !string.IsNullOrWhiteSpace(Entry.Url);

    public string DocsButtonText
    {
        get => _docsButtonText;
        private set => SetField(ref _docsButtonText, value);
    }

    public string GroupKey => Entry.Group;

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string ActionText
    {
        get => _actionText;
        set => SetField(ref _actionText, value);
    }

    public bool ActionEnabled
    {
        get => _actionEnabled;
        set => SetField(ref _actionEnabled, value);
    }

    public bool IsPresent
    {
        get => _isPresent;
        set => SetField(ref _isPresent, value);
    }

    public ICommand PrimaryCommand { get; }
    public ICommand DocsCommand { get; }

    public event Action<ToolHubRow>? PrimaryRequested;
    public event Action<ToolHubRow>? DocsRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLocalizedChrome()
    {
        DocsButtonText = Loc.T("tools.hub.docs");
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Improves));
        OnPropertyChanged(nameof(RiskNote));
        OnPropertyChanged(nameof(HasRiskNote));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public sealed class ToolHubGroupRow
{
    public ToolHubGroupRow(string groupKey, string title, IReadOnlyList<ToolHubRow> items)
    {
        GroupKey = groupKey;
        Title = title;
        Items = items;
    }

    public string GroupKey { get; }
    public string Title { get; }
    public IReadOnlyList<ToolHubRow> Items { get; }
}

internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
