﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;


namespace Microsoft.IoT.ModelsRepository.CommandLine.Tests
{
    internal class ClientInvokator
    {
        readonly static string cliProjectPath = Path.GetFullPath(@"../../../../Microsoft.IoT.ModelsRepository.CommandLine");

        public static (int, string, string) Invoke(string commandArgs)
        {
            string moniker = GetFrameworkMoniker();
            var cmdsi = new ProcessStartInfo("dotnet")
            {
                Arguments = $"run {GetBuildParam()} --framework {moniker} -- {commandArgs}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = cliProjectPath
            };

            Process cmd = Process.Start(cmdsi);
            string standardOut = cmd.StandardOutput.ReadToEnd();
            string standardError = cmd.StandardError.ReadToEnd();

            cmd.WaitForExit(10000);
            return (cmd.ExitCode, standardOut, standardError);
        }

        public static string GetFrameworkMoniker()
        {
            string lframeworkDesc = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.ToLower();
            if (lframeworkDesc.StartsWith(".net core 3.1"))
            {
                return "netcoreapp3.1";
            }

            if (lframeworkDesc.StartsWith(".net 5.0"))
            {
                return "net5.0";
            }

            throw new ArgumentException($"Unsupported framework: {lframeworkDesc}.");
        }

        private static string GetBuildParam()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOT_MODELSREPOSITORY_PIPELINE_BUILD")))
            {
                return "--no-build";
            }

            return "--no-restore";
        }
    }
}
