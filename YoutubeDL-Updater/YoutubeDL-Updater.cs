﻿using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Policy;

namespace YoutubeDLUpdater
{
    public class YoutubeDLUpdater : NeosMod
    {
        public override string Name => "YoutubeDL-Updater";
        public override string Author => "dfgHiatus and Frozen";
        public override string Version => "1.0.1";
        public override string Link => "https://github.com/dfgHiatus/YoutubeDL-Updater";

        private static ModConfiguration _modConfig;

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(1, 0, 1))
                .AutoSave(true);
        }

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> _runOnStartup = new ModConfigurationKey<bool>("run_on_startup", "Check for updates on startup", () => true);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> _useDefaultYoutubeDl = new ModConfigurationKey<bool>("use_default_youtubeDL", "Use Neos's default YoutubeDL (requires restart)", () => false);

        private static readonly Url PathSha256 = new Url("https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS");
        private static readonly Url PathYtDlp = new Url("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");
        private static readonly string ExecutablePath = Path.Combine(Engine.Current.AppPath, "RuntimeData");
        private static readonly string BackupPath = Path.Combine(ExecutablePath, "YoutubeDL-Backups");
        private const string ExecutableName = "yt-dlp.exe";
        
        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.YoutubeDLUpdater").PatchAll();
            _modConfig = GetConfiguration();
            EnsureBackup();
            if (_modConfig == null) return;
            if (_modConfig.GetValue(_useDefaultYoutubeDl))
            {
                LoadBackup();
                return;
            }
            if (_modConfig.GetValue(_runOnStartup)) UpdateYoutubeDL();
        }

        private static void EnsureBackup()
        {
            Directory.CreateDirectory(BackupPath);
            // See if we already have a backup
            if (Directory.GetFiles(BackupPath).Length > 0) return;
            // If we don't have a backup (first time!), make it
            File.Copy(Path.Combine(ExecutablePath, ExecutableName), Path.Combine(BackupPath, ExecutableName));
        }
        
        private static void LoadBackup()
        {
            // Get the SHA256 of the backup and the current executable
            var backupSha256 = ComputeSha256(Path.Combine(BackupPath, ExecutableName));
            var currentSha256 = ComputeSha256(Path.Combine(ExecutablePath, ExecutableName));
            if (backupSha256 != currentSha256)
                File.Copy(Path.Combine(BackupPath, ExecutableName), Path.Combine(ExecutablePath, ExecutableName));
            // modConfig.Set(useDefaultYoutubeDL, false);
        }

        private static void UpdateYoutubeDL()
        {
            // Download the text file at PATH_SHA_256. This might throw, so we need to wrap it
            string sha256;
            try
            {
                using (var client = new WebClient())
                    sha256 = client.DownloadString(PathSha256.Value);
            }
            catch (Exception e)
            {
                Error("An error occured while downloading SHA256 hashes from yt-dlp's github.");
                Error(e.Message);
                return;
            }
            if (string.IsNullOrEmpty(sha256))
            {
                Error("There were no SHA256 hashes from yt-dlp's github. Aborting update.");
                return;
            }
            // Split the string on newlines
            var allLines = sha256.Split('\n');
            // Get the entry that ends with "yt-dlp.exe"
            var line = Array.Find(allLines, x => x.EndsWith(ExecutableName));
            // We now have a string that looks like "<SHA256>  yt-dlp.exe", with 2 spaces(?!). To get the hash, we remove the executable name plus 2 spaces
            var stringHash = line.Remove(line.Length - (ExecutableName.Length + 2));
            // Compute the SHA256 of yt-dlp.exe in the above directory
            var computedHash = ComputeSha256(Path.Combine(ExecutablePath, ExecutableName));
            // If these 2 are equal, then we have the latest build
            if (stringHash == computedHash)
            {
                Msg($"yt-dlp.exe is up to date.");
                return;
            }
            Msg($"Local yt-dlp.exe is {computedHash} but remote is {stringHash}! Downloading the latest version...");
            // Download the binary file at PATH_YT_DLP. This might throw, so we need to wrap it again
            try
            {
                using (var client = new WebClient())
                    client.DownloadFile(PathYtDlp.Value, Path.Combine(ExecutablePath, ExecutableName));
            }
            catch (Exception e)
            {
                Error("An error occured while downloading yt-dlp.exe from github.");
                Error(e.Message);
                return;
            }
            Msg($"Download complete! yt-dlp.exe is now up to date.");
        }
        
        // https://stackoverflow.com/questions/38474362/get-a-file-sha256-hash-code-and-checksum with corrections
        private static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var fileStream = File.OpenRead(filePath))
                return BitConverter.ToString(sha256.ComputeHash(fileStream)).Replace("-", "").ToLowerInvariant();
        }
    }
}