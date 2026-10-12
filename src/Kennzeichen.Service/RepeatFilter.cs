namespace Kennzeichen.Service;

/// <summary>Unterdrückt dasselbe Kennzeichen derselben Kamera innerhalb eines Zeitfensters.</summary>
internal sealed class RepeatFilter(TimeSpan window)
{
    private readonly Dictionary<(string Source, string Text), DateTimeOffset> _lastSeen = [];

    /// <returns><c>true</c>, wenn das Kennzeichen gemeldet werden soll.</returns>
    public bool ShouldReport(string sourceId, string text, DateTimeOffset timestamp)
    {
        if (window <= TimeSpan.Zero)
        {
            return true;
        }

        var key = (sourceId, text);
        var report = !_lastSeen.TryGetValue(key, out var last) || timestamp - last >= window;

        // Auch unterdrückte Sichtungen verlängern das Fenster: ein stehendes Auto wird nur einmal gemeldet.
        _lastSeen[key] = timestamp;
        Prune(timestamp);
        return report;
    }

    private void Prune(DateTimeOffset now)
    {
        if (_lastSeen.Count < 1024)
        {
            return;
        }

        foreach (var key in _lastSeen.Where(e => now - e.Value >= window).Select(e => e.Key).ToList())
        {
            _lastSeen.Remove(key);
        }
    }
}
