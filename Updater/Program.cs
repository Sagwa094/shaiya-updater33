﻿using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Updater.Common;
using Updater.Core;
using Updater.Extensions;
using Updater.Helpers;
using Updater.Interop;
using Updater.Resources;

namespace Updater
{
    public static class Program
    {
        public static void DoWork(HttpClient httpClient, BackgroundWorker worker)
        {
            try
            {
                var serverConfiguration = new ServerConfiguration(httpClient);
                var clientConfiguration = new ClientConfiguration();

                if (serverConfiguration.UpdaterVersion > Constants.UpdaterVersion)
                {
                    worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage1));
                    UpdaterPatcher(httpClient);
                    return;
                }

                if (serverConfiguration.PatchFileVersion > clientConfiguration.CurrentVersion)
                {
                    worker.ReportProgress(0, new ProgressReport(1));
                    worker.ReportProgress(0, new ProgressReport(2));

                    uint progressMax = serverConfiguration.PatchFileVersion - clientConfiguration.CurrentVersion;
                    uint progressValue = 1;

                    while (clientConfiguration.CurrentVersion < serverConfiguration.PatchFileVersion)
                    {
                        var progressMessage = string.Format(Strings.ProgressMessage2, progressValue, progressMax);
                        worker.ReportProgress(0, new ProgressReport(progressMessage));

                        var patch = new Patch(clientConfiguration.CurrentVersion + 1);
                        httpClient.DownloadFile(patch.Url, patch.Path);

                        if (!patch.Exists())
                        {
                            worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage3));
                            return;
                        }

                        worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage4));
                        IniHelper.WritePrivateProfileString("Version", "StartUpdate", "EXTRACT_START", clientConfiguration.Path);

                        // Issue: antivirus software could be scanning a file from a previous patch
                        // when this method tries to overwrite it.
                        if (!patch.ExtractToCurrentDirectory())
                        {
                            worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage5));
                            return;
                        }

                        IniHelper.WritePrivateProfileString("Version", "StartUpdate", "EXTRACT_END", clientConfiguration.Path);
                        patch.Delete();

                        worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage6));
                        IniHelper.WritePrivateProfileString("Version", "StartUpdate", "UPDATE_START", clientConfiguration.Path);

                        DataPatcher(worker);

                        IniHelper.WritePrivateProfileString("Version", "StartUpdate", "UPDATE_END", clientConfiguration.Path);

                        ++clientConfiguration.CurrentVersion;
                        ++progressValue;

                        var currentVersion = clientConfiguration.CurrentVersion;
                        var percentProgress = MathHelper.CalculatePercentage((int)currentVersion, (int)serverConfiguration.PatchFileVersion);
                        if (percentProgress > 0)
                            worker.ReportProgress(percentProgress, new ProgressReport(Strings.ProgressMessage7, 2));

                        IniHelper.WritePrivateProfileString("Version", "CurrentVersion", currentVersion.ToString(), clientConfiguration.Path);
                    }

                    worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage8));
                    DataBuilder(worker);
                    worker.ReportProgress(0, new ProgressReport(Strings.ProgressMessage7));
                }
            }
            catch (Exception ex)
            {
                var caption = Application.ResourceAssembly.GetName().Name;
                MessageBox.Show(ex.ToString(), caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worker"></param>
        private static void DataBuilder(BackgroundWorker worker)
        {
            if (!File.Exists("data.sah") || !File.Exists("data.saf"))
                return;

            var binaryReader = new BinaryReader(File.OpenRead("data.sah"));
            binaryReader.BaseStream.Seek(7, SeekOrigin.Begin);

            var fileCount = binaryReader.ReadInt32();
            binaryReader.Close();

            var progressReport = new ProgressReport(1);
            var progress = new Progress(worker, progressReport, fileCount, 1);
            Function.DataBuilder(progress.PerformStep);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worker"></param>
        private static void DataPatcher(BackgroundWorker worker)
        {
            if (File.Exists("delete.lst"))
            {
                var paths = File.ReadAllLines("delete.lst");
                if (paths.Length > 0)
                {
                    var progressReport = new ProgressReport(1);
                    var progress = new Progress(worker, progressReport, paths.Length, 1);
                    Function.RemoveFiles(progress.PerformStep);
                }
            }

            if (File.Exists("update.sah") && File.Exists("update.saf"))
            {
                var binaryReader = new BinaryReader(File.OpenRead("update.sah"));
                binaryReader.BaseStream.Seek(7, SeekOrigin.Begin);

                var fileCount = binaryReader.ReadInt32();
                binaryReader.Close();

                var progressReport = new ProgressReport(1);
                var progress = new Progress(worker, progressReport, fileCount, 1);
                Function.DataPatcher(progress.PerformStep);
            }
        }

        /// <summary>
        /// Downloads a new updater, starts a client process, passing "new updater" as the 
        /// command-line argument, and terminates the current process.
        /// 
        /// Expect the client to delete the old updater, rename the new updater and create 
        /// an updater process.
        /// </summary>
        /// <param name="httpClient"></param>
        private static void UpdaterPatcher(HttpClient httpClient)
        {
            var newUpdater = new NewUpdater();
            httpClient.DownloadFile(newUpdater.Url, newUpdater.Path);

            if (!File.Exists(newUpdater.Path))
                return;

            var fileName = Path.Combine(Directory.GetCurrentDirectory(), "game.exe");
            Process.Start(fileName, "new updater");
            _ = DllImport.TerminateProcess(DllImport.GetCurrentProcess(), 0);
        }
    }
}
