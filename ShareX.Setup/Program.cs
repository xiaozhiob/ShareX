﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2022 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Diagnostics;
using System.IO;

namespace ShareX.Setup
{
    internal class Program
    {
        [Flags]
        private enum SetupJobs
        {
            None = 0,
            CreateSetup = 1,
            CreatePortable = 1 << 1,
            CreateDebug = 1 << 2,
            CreateSteamFolder = 1 << 3,
            CreateMicrosoftStoreFolder = 1 << 4,
            CreateMicrosoftStoreDebugFolder = 1 << 5,
            CompileAppx = 1 << 6,
            DownloadFFmpeg = 1 << 7,
            CreateChecksumFile = 1 << 8,
            OpenOutputDirectory = 1 << 9,

            Release = CreateSetup | CreatePortable | DownloadFFmpeg | CreateChecksumFile | OpenOutputDirectory,
            Debug = CreateDebug | DownloadFFmpeg | CreateChecksumFile | OpenOutputDirectory,
            Steam = CreateSteamFolder | DownloadFFmpeg | CreateChecksumFile | OpenOutputDirectory,
            MicrosoftStore = CreateMicrosoftStoreFolder | CompileAppx | DownloadFFmpeg | CreateChecksumFile | OpenOutputDirectory,
            MicrosoftStoreDebug = CreateMicrosoftStoreDebugFolder | CompileAppx | DownloadFFmpeg | CreateChecksumFile | OpenOutputDirectory
        }

        private static SetupJobs Job = SetupJobs.Release;
        private static bool AppVeyor = false;

        private static string Configuration
        {
            get
            {
                if (Job.HasFlag(SetupJobs.CreateDebug))
                {
                    return "Debug";
                }
                else if (Job.HasFlag(SetupJobs.CreateSteamFolder))
                {
                    return "Steam";
                }
                else if (Job.HasFlag(SetupJobs.CreateMicrosoftStoreFolder))
                {
                    return "MicrosoftStore";
                }
                else if (Job.HasFlag(SetupJobs.CreateMicrosoftStoreDebugFolder))
                {
                    return "MicrosoftStoreDebug";
                }

                return "Release";
            }
        }

        private static string ParentDir => AppVeyor ? "." : @"..\..\..\";
        private static string BinDir => Path.Combine(ParentDir, "ShareX", "bin", Configuration);
        private static string NativeMessagingHostDir => Path.Combine(ParentDir, "ShareX.NativeMessagingHost", "bin", Configuration);
        private static string SteamLauncherDir => Path.Combine(ParentDir, "ShareX.Steam", "bin", Configuration);
        private static string ExecutablePath => Path.Combine(BinDir, "ShareX.exe");

        private static string OutputDir => Path.Combine(ParentDir, "Output");
        private static string PortableOutputDir => Path.Combine(OutputDir, "ShareX-portable");
        private static string DebugOutputDir => Path.Combine(OutputDir, "ShareX-debug");
        private static string SteamOutputDir => Path.Combine(OutputDir, "ShareX-Steam");
        private static string MicrosoftStoreOutputDir => Path.Combine(OutputDir, "ShareX-MicrosoftStore");

        private static string SetupDir => Path.Combine(ParentDir, "ShareX.Setup");
        private static string InnoSetupDir => Path.Combine(SetupDir, "InnoSetup");
        private static string MicrosoftStorePackageFilesDir => Path.Combine(SetupDir, "MicrosoftStore");

        private static string SetupPath => Path.Combine(OutputDir, $"ShareX-{AppVersion}-setup.exe");
        private static string RecorderDevicesSetupPath => Path.Combine(OutputDir, "Recorder-devices-setup.exe");
        private static string PortableZipPath => Path.Combine(OutputDir, $"ShareX-{AppVersion}-portable.zip");
        private static string DebugZipPath => Path.Combine(OutputDir, $"ShareX-{AppVersion}-debug.zip");
        private static string SteamUpdatesDir => Path.Combine(SteamOutputDir, "Updates");
        private static string SteamZipPath => Path.Combine(OutputDir, $"ShareX-{AppVersion}-Steam.zip");
        private static string MicrosoftStoreAppxPath => Path.Combine(OutputDir, $"ShareX-{AppVersion}.appx");
        private static string FFmpegPath => Path.Combine(OutputDir, "ffmpeg.exe");

        private static string InnoSetupCompilerPath = @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe";
        private static string MakeAppxPath = @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.18362.0\x64\makeappx.exe";
        private static string AppVersion;

        private static void Main(string[] args)
        {
            Console.WriteLine("ShareX setup started.");

            CheckArgs(args);

            Console.WriteLine("Setup job: " + Job);

            AppVersion = GetAppVersion();

            if (Directory.Exists(OutputDir))
            {
                Console.WriteLine("Cleaning output directory: " + OutputDir);

                Directory.Delete(OutputDir, true);
            }

            if (Job.HasFlag(SetupJobs.DownloadFFmpeg))
            {
                DownloadFFmpeg();
            }

            if (Job.HasFlag(SetupJobs.CreateSetup))
            {
                CompileSetup();
            }

            if (Job.HasFlag(SetupJobs.CreatePortable))
            {
                CreateFolder(BinDir, PortableOutputDir, SetupJobs.CreatePortable);
            }

            if (Job.HasFlag(SetupJobs.CreateDebug))
            {
                CreateFolder(BinDir, DebugOutputDir, SetupJobs.CreateDebug);
            }

            if (Job.HasFlag(SetupJobs.CreateSteamFolder))
            {
                CreateSteamFolder();
            }

            if (Job.HasFlag(SetupJobs.CreateMicrosoftStoreFolder))
            {
                CreateFolder(BinDir, MicrosoftStoreOutputDir, SetupJobs.CreateMicrosoftStoreFolder);
            }

            if (Job.HasFlag(SetupJobs.CreateMicrosoftStoreDebugFolder))
            {
                CreateFolder(BinDir, MicrosoftStoreOutputDir, SetupJobs.CreateMicrosoftStoreDebugFolder);
            }

            if (Job.HasFlag(SetupJobs.CompileAppx))
            {
                CompileAppx();
            }

            if (AppVeyor)
            {
                FileHelpers.CopyAll(OutputDir, ParentDir);
            }

            if (Job.HasFlag(SetupJobs.OpenOutputDirectory) && !AppVeyor)
            {
                FileHelpers.OpenFolder(OutputDir, false);
            }

            Console.WriteLine("ShareX setup successfully completed.");
        }

        private static void CheckArgs(string[] args)
        {
            CLIManager cli = new CLIManager(args);
            cli.ParseCommands();

            CLICommand command = cli.GetCommand("AppVeyor");

            if (command != null)
            {
                AppVeyor = true;

                string configuration = command.Parameter;

                Console.WriteLine("AppVeyor: " + configuration);

                if (configuration.Equals("Release", StringComparison.OrdinalIgnoreCase))
                {
                    Job = SetupJobs.Release;
                }
                else if (configuration.Equals("Debug", StringComparison.OrdinalIgnoreCase))
                {
                    Job = SetupJobs.Debug;
                }
                else if (configuration.Equals("Steam", StringComparison.OrdinalIgnoreCase))
                {
                    Job = SetupJobs.Steam;
                }
                else if (configuration.Equals("MicrosoftStore", StringComparison.OrdinalIgnoreCase))
                {
                    Job = SetupJobs.MicrosoftStore;
                }
                else
                {
                    Environment.Exit(0);
                }
            }
        }

        private static string GetAppVersion()
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(ExecutablePath);
            return $"{versionInfo.ProductMajorPart}.{versionInfo.ProductMinorPart}.{versionInfo.ProductBuildPart}";
        }

        private static void CompileSetup()
        {
            CompileISSFile("Recorder-devices-setup.iss");
            CompileISSFile("ShareX-setup.iss");
            CreateChecksumFile(SetupPath);
        }

        private static void CompileISSFile(string fileName)
        {
            if (File.Exists(InnoSetupCompilerPath))
            {
                Console.WriteLine("Compiling setup file: " + fileName);

                using (Process process = new Process())
                {
                    ProcessStartInfo psi = new ProcessStartInfo()
                    {
                        FileName = InnoSetupCompilerPath,
                        WorkingDirectory = Path.GetFullPath(InnoSetupDir),
                        Arguments = $"\"{fileName}\"",
                        UseShellExecute = false
                    };

                    process.StartInfo = psi;
                    process.Start();
                    process.WaitForExit();
                }

                Console.WriteLine("Setup file compiled.");
            }
            else
            {
                Console.WriteLine("InnoSetup compiler is missing: " + InnoSetupCompilerPath);
            }
        }

        private static void CompileAppx()
        {
            Console.WriteLine("Compiling appx file: " + MicrosoftStoreAppxPath);

            using (Process process = new Process())
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = MakeAppxPath,
                    Arguments = $"pack /d \"{MicrosoftStoreOutputDir}\" /p \"{MicrosoftStoreAppxPath}\" /l /o",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
                process.StartInfo = psi;
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }

            Console.WriteLine("Appx file compiled.");

            CreateChecksumFile(MicrosoftStoreAppxPath);
        }

        private static void CreateSteamFolder()
        {
            Console.WriteLine("Creating Steam folder: " + SteamOutputDir);

            if (Directory.Exists(SteamOutputDir))
            {
                Directory.Delete(SteamOutputDir, true);
            }

            Directory.CreateDirectory(SteamOutputDir);

            SetupHelpers.CopyFile(Path.Combine(SteamLauncherDir, "ShareX_Launcher.exe"), SteamOutputDir);
            SetupHelpers.CopyFile(Path.Combine(SteamLauncherDir, "steam_appid.txt"), SteamOutputDir);
            SetupHelpers.CopyFile(Path.Combine(SteamLauncherDir, "installscript.vdf"), SteamOutputDir);
            SetupHelpers.CopyFiles(SteamLauncherDir, "*.dll", SteamOutputDir);

            CreateFolder(BinDir, SteamUpdatesDir, SetupJobs.CreateSteamFolder);
        }

        private static void CreateFolder(string source, string destination, SetupJobs job)
        {
            Console.WriteLine("Creating folder: " + destination);

            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);

            SetupHelpers.CopyFile(Path.Combine(source, "ShareX.exe"), destination);
            SetupHelpers.CopyFile(Path.Combine(source, "ShareX.exe.config"), destination);
            SetupHelpers.CopyFiles(source, "*.dll", destination);

            if (job == SetupJobs.CreateDebug || job == SetupJobs.CreateMicrosoftStoreDebugFolder)
            {
                SetupHelpers.CopyFiles(source, "*.pdb", destination);
            }

            SetupHelpers.CopyFiles(Path.Combine(ParentDir, "Licenses"), "*.txt", Path.Combine(destination, "Licenses"));

            if (job != SetupJobs.CreateMicrosoftStoreFolder && job != SetupJobs.CreateMicrosoftStoreDebugFolder)
            {
                if (!File.Exists(RecorderDevicesSetupPath))
                {
                    CompileISSFile("Recorder-devices-setup.iss");
                }

                SetupHelpers.CopyFile(RecorderDevicesSetupPath, destination);

                SetupHelpers.CopyFile(Path.Combine(NativeMessagingHostDir, "ShareX_NativeMessagingHost.exe"), destination);
            }

            string[] languages = new string[] { "de", "es", "es-MX", "fa-IR", "fr", "hu", "id-ID", "it-IT", "ja-JP", "ko-KR", "nl-NL", "pl", "pt-BR", "pt-PT",
                "ro", "ru", "tr", "uk", "vi-VN", "zh-CN", "zh-TW" };

            foreach (string language in languages)
            {
                SetupHelpers.CopyFiles(Path.Combine(source, language), "*.resources.dll", Path.Combine(destination, "Languages", language));
            }

            if (File.Exists(FFmpegPath))
            {
                SetupHelpers.CopyFile(FFmpegPath, destination);
            }

            FileHelpers.CopyAll(Path.Combine(ParentDir, @"ShareX.ScreenCaptureLib\Stickers"), Path.Combine(destination, "Stickers"));

            if (job == SetupJobs.CreatePortable)
            {
                FileHelpers.CreateEmptyFile(Path.Combine(destination, "Portable"));
                CreateZipFile(destination, PortableZipPath);
            }
            else if (job == SetupJobs.CreateDebug)
            {
                CreateZipFile(destination, DebugZipPath);
            }
            else if (job == SetupJobs.CreateSteamFolder)
            {
                CreateZipFile(destination, SteamZipPath);
            }
            else if (job == SetupJobs.CreateMicrosoftStoreFolder || job == SetupJobs.CreateMicrosoftStoreDebugFolder)
            {
                FileHelpers.CopyAll(MicrosoftStorePackageFilesDir, destination);
            }

            Console.WriteLine("Folder created.");
        }

        private static void CreateZipFile(string source, string archivePath)
        {
            Console.WriteLine("Creating zip file: " + archivePath);

            ZipManager.Compress(Path.GetFullPath(source), Path.GetFullPath(archivePath));
            CreateChecksumFile(archivePath);
        }

        private static void DownloadFFmpeg()
        {
            if (!File.Exists(FFmpegPath))
            {
                string filePath = SetupHelpers.DownloadFile("https://github.com/ShareX/FFmpeg/releases/download/v5.1/ffmpeg-5.1-win64.zip");
                ZipManager.Extract(filePath, OutputDir, false, entry => entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase), 200_000_000);
            }
        }

        private static void CreateChecksumFile(string filePath)
        {
            if (Job.HasFlag(SetupJobs.CreateChecksumFile))
            {
                Console.WriteLine("Creating checksum file: " + filePath);

                Helpers.CreateChecksumFile(filePath);
            }
        }
    }
}