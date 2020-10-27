﻿using Azure.IoT.DeviceModelsRepository.Resolver;
using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.IoT.DeviceModelsRepository.CLI
{
    class Outputs
    {
        private static readonly string _parserVersion = typeof(ModelParser).Assembly.GetName().Version.ToString();
        private static readonly string _resolverVersion = typeof(ResolverClient).Assembly.GetName().Version.ToString();
        private static readonly string _cliVersion = typeof(Program).Assembly.GetName().Version.ToString();

        public async static Task WriteErrorAsync(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            await Console.Error.WriteLineAsync(msg);
            Console.ResetColor();
        }

        public async static Task WriteHeadersAsync()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Out.WriteLineAsync($"dmr-client/{_cliVersion} parser/{_parserVersion} resolver/{_resolverVersion}");
            Console.ResetColor();
        }

        public async static Task WriteInputsAsync(string command, Dictionary<string, string> inputs)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            StringBuilder builder = new StringBuilder();
            builder.Append($"{command}");
            foreach (var item in inputs)
            {
                if (item.Value != null)
                {
                    builder.Append($" --{item.Key} {item.Value}");
                }
            }
            await Console.Out.WriteLineAsync($"{builder}{Environment.NewLine}");
            Console.ResetColor();
        }
    }
}
