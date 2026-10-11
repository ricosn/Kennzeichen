using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kennzeichen.Abstractions;

/// <summary>Ein Erkennungsalgorithmus. Implementierungen müssen nicht threadsicher sein.</summary>
public interface IPlateRecognizer : IDisposable
{
    Task<IReadOnlyList<PlateReading>> RecognizeAsync(Frame frame, CancellationToken cancellationToken);
}

/// <summary>
/// Einstiegspunkt eines Erkenner-Plugins. Der Dienst sucht in jeder Plugin-DLL nach öffentlichen,
/// nicht abstrakten Klassen mit diesem Interface und einem parameterlosen Konstruktor.
/// </summary>
public interface IPlateRecognizerFactory
{
    /// <summary>Eindeutiger Name, über den der Erkenner in der Konfiguration ausgewählt wird.</summary>
    string Name { get; }

    /// <param name="settings">Der Abschnitt <c>Recognizer:Settings</c> aus der Dienstkonfiguration.</param>
    /// <param name="baseDirectory">Verzeichnis, gegen das relative Pfade in <paramref name="settings"/> aufgelöst werden.</param>
    IPlateRecognizer Create(IConfiguration settings, string baseDirectory, ILoggerFactory loggerFactory);
}
