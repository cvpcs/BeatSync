using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BeatSyncLib.Hashing
{
    public interface ISongHashCollection : IReadOnlyCollection<string>
    {
        public event EventHandler<int>? CollectionRefreshed;

        public HashingState HashingState { get; }
        public int CountMissing { get; }

        public Task<int> RefreshHashesAsync(CancellationToken cancellationToken);
        public Task<int> RefreshHashesAsync(bool missingOnly, CancellationToken cancellationToken);
        public Task<int> RefreshHashesAsync(bool missingOnly, IProgress<double>? progress, CancellationToken cancellationToken);

        public bool HashExists(string hash);
        public FileSystemInfo GetSongLocation(string hash);
    }

    public enum HashingState
    {
        NotStarted,
        InProgress,
        Finished
    }
}
