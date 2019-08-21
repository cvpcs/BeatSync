﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BeatSync;
using BeatSync.Logging;
using BeatSync.Playlists;
using BeatSync.Configs;
using System.IO;

namespace BeatSyncTests.SongDownloader_Tests
{
    [TestClass]
    public class DownloadJob_Tests
    {
        
        static DownloadJob_Tests()
        {
            TestSetup.Initialize();
            defaultConfig = new PluginConfig().SetDefaults();
            SongFeedReaders.WebUtils.Initialize(new WebUtilities.WebWrapper.WebClientWrapper());
        }

        public static PluginConfig defaultConfig;
        public static string DefaultHistoryPath = Path.GetFullPath(Path.Combine("Output", "DownloadJob_Tests", "BeatSyncHistory.json"));
        public static string DefaultHashCachePath = Path.GetFullPath(Path.Combine("Output", "DownloadJob_Tests", "hashData.dat"));
        public static string DefaultSongsPath = Path.GetFullPath(Path.Combine("Output", "DownloadJob_Tests", "Songs"));

        [TestMethod]
        public void SongDoesntExist()
        {

            var historyManager = new HistoryManager(DefaultHistoryPath);
            var songHasher = new SongHasher(DefaultSongsPath, DefaultHashCachePath);
            historyManager.Initialize();
            var downloader = new SongDownloader(defaultConfig, historyManager, songHasher, DefaultSongsPath);
            var doesntExist = new PlaylistSong("196be1af64958d8b5375b328b0eafae2151d46f8", "Doesn't Exist", "ffff", "Who knows");
            historyManager.TryAdd(doesntExist); // Song is added before it gets to DownloadJob
            var result = downloader.DownloadJob(doesntExist).Result;
            Assert.IsTrue(historyManager.ContainsKey(doesntExist.Hash)); // Keep song in history so it doesn't try to download a non-existant song again.
        }
    }
}
