using System.Threading.Tasks;

namespace CloudNotes.Desktop.Services;

public interface ISyncService
{
    Task<bool> SyncAsync();

    Task<bool> SyncOnStartupAsync();

    void StartPeriodicSync();

    void StopPeriodicSync();
}

