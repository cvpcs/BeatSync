﻿using BeatSaber.SongHashing;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BeatSyncLib.Hashing
{
    public class DirectoryHasher : ISongHashCollection
    {
        protected IBeatmapHasher Hasher;
        public event EventHandler<int>? CollectionRefreshed;

        private ConcurrentDictionary<string, HashResult> Hashes = new ConcurrentDictionary<string, HashResult>(StringComparer.OrdinalIgnoreCase);
        private ConcurrentDictionary<string, FileSystemInfo> SongLocationMap = new ConcurrentDictionary<string, FileSystemInfo>(StringComparer.OrdinalIgnoreCase);
        public string DirectoryPath => _directory.FullName;
        private DirectoryInfo _directory { get; }
        public HashingState HashingState { get; private set; }
        public int Count => Hashes.Count;
        public int CountMissing => TotalBeatmaps - Hashes.Count;

        private int IgnoredDirectories = 0;
        private int TotalBeatmaps
        {
            get
            {
                try
                {
                    if (_directory.Exists)
                    {
                        return _directory.GetDirectories().Length + _directory.GetFiles("*.zip").Length;
                    }
                }
                catch { }
                return 0;
            }
        }

        public DirectoryHasher(string directoryPath)
        {
            _directory = new DirectoryInfo(directoryPath ?? throw new ArgumentNullException(nameof(directoryPath)));
            Hasher = new Hasher();
        }
        public DirectoryHasher(string directoryPath, IBeatmapHasher hasher)
        {
            _directory = new DirectoryInfo(directoryPath ?? throw new ArgumentNullException(nameof(directoryPath)));
            Hasher = hasher;
        }

        public Task<int> RefreshHashesAsync(CancellationToken cancellationToken) => RefreshHashesAsync(false, null, CancellationToken.None);

        public Task<int> RefreshHashesAsync(bool missingOnly, CancellationToken cancellationToken) => RefreshHashesAsync(missingOnly, null, cancellationToken);

        public async Task<int> RefreshHashesAsync(bool missingOnly, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (!_directory.Exists)
                throw new HashingTargetNotFoundException($"Directory does not exist: {DirectoryPath}");
            IBeatmapHasher hasher = Hasher;
            HashingState = HashingState.InProgress;
            HashResult[] dirResults = await HashWithTasksAsync(hasher, progress, cancellationToken).ConfigureAwait(false);
            HashingState = HashingState.Finished;
            CollectionRefreshed?.Invoke(this, dirResults.Length);
            return dirResults.Length;
        }

        private Task<HashResult[]> HashWithTasksAsync(IBeatmapHasher hasher, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            int beatmapCount = TotalBeatmaps;
            int count = 0;
            var dirTasks = _directory.EnumerateDirectories()
                .Select(async dir =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return new HashResult(null, "The task was cancelled.", null);
                    HashResult hashResult = await Task.Run(() => hasher.HashDirectory(dir.FullName, cancellationToken)).ConfigureAwait(false);
                    if (hashResult.ResultType == HashResultType.Error)
                        Logger.log?.Warn($"Unable to get hash for '{dir.Name}': {hashResult.Message}.");
                    else if (hashResult.ResultType == HashResultType.Warn)
                        Logger.log?.Warn($"Hash warning for '{dir.Name}': {hashResult.Message}.");
                    if (hashResult.Hash != null && hashResult.Hash.Length > 0)
                    {
                        if (!Hashes.TryAdd(hashResult.Hash, hashResult))
                            Logger.log?.Debug($"Duplicate beatmap: {dir.Name} | {hashResult.Hash}");
                        else
                            SongLocationMap[hashResult.Hash] = dir;
                    }
                    if (progress != null)
                    {
                        int currentCount = Interlocked.Increment(ref count);
                        progress.Report(currentCount / (double)beatmapCount);
                    }
                    return hashResult;
                }).ToArray();

            var zipTasks = _directory.EnumerateFiles("*.zip")
                .Select(async zipFile =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return new HashResult(null, "The task was cancelled.", null);
                    HashResult hashResult = await Task.Run(() => hasher.HashZippedBeatmap(zipFile.FullName, cancellationToken)).ConfigureAwait(false);
                    if (hashResult.ResultType == HashResultType.Error)
                        Logger.log?.Warn($"Unable to get hash for '{zipFile.Name}': {hashResult.Message}.");
                    else if (hashResult.ResultType == HashResultType.Warn)
                        Logger.log?.Warn($"Hash warning for '{zipFile.Name}': {hashResult.Message}.");
                    if (hashResult.Hash != null && hashResult.Hash.Length > 0)
                    {
                        if (!Hashes.TryAdd(hashResult.Hash, hashResult))
                            Logger.log?.Debug($"Duplicate beatmap: {zipFile.Name} | {hashResult.Hash}");
                        else
                            SongLocationMap[hashResult.Hash] = zipFile;
                    }
                    if (progress != null)
                    {
                        int currentCount = Interlocked.Increment(ref count);
                        progress.Report(currentCount / (double)beatmapCount);
                    }
                    return hashResult;
                }).ToArray();
            return Task.WhenAll(dirTasks.Concat(zipTasks));
        }

        private Task<HashResult[]> HashWithParallelAsync(IBeatmapHasher hasher, CancellationToken cancellationToken)
        {
            Console.WriteLine("Hashing with AsParallel");
            return Task.Run(() => _directory.EnumerateDirectories()
                .AsParallel()
                .Select(dir =>
                {

                    HashResult hashResult = hasher.HashDirectory(dir.FullName, cancellationToken);
                    if (hashResult.ResultType == HashResultType.Error)
                        Logger.log?.Warn($"Unable to get hash for '{dir.Name}': {hashResult.Message}.");
                    else if (hashResult.ResultType == HashResultType.Warn)
                        Logger.log?.Warn($"Hash warning for '{dir.Name}': {hashResult.Message}.");
                    if (hashResult.Hash != null && hashResult.Hash.Length > 0)
                    {
                        Hashes[hashResult.Hash] = hashResult;
                        SongLocationMap[hashResult.Hash] = dir;
                    }

                    return hashResult;
                }).ToArray());
        }

        public bool HashExists(string hash) => Hashes.ContainsKey(hash);

        public FileSystemInfo GetSongLocation(string hash) => HashExists(hash) ? SongLocationMap[hash] : null;

        public IEnumerator<string> GetEnumerator() => Hashes.Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Hashes.Keys.GetEnumerator();
    }
}
