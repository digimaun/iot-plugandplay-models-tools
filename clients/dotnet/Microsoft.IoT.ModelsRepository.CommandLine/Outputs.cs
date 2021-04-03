using Azure.IoT.ModelsRepository;
using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Microsoft.IoT.ModelsRepository.CommandLine
{
    internal class Outputs
    {
        public static readonly string ParserVersion = FileVersionInfo.GetVersionInfo(typeof(ModelParser).Assembly.Location).ProductVersion;
        public static readonly string RepositoryClientVersion = FileVersionInfo.GetVersionInfo(typeof(ModelsRepositoryClient).Assembly.Location).ProductVersion;
        public static readonly string CommandLineVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        public static readonly string DebugHeader = $"dmr-client/{CommandLineVersion} parser/{ParserVersion} sdk/{RepositoryClientVersion}";

        public static void WriteError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {msg}");
            Console.ResetColor();
        }

        public static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine(DebugHeader);
            Console.ResetColor();
        }

        public static void WriteOut(string content, ConsoleColor? color=null)
        {
            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
            }

            Console.Out.WriteLine(content);

            if (color.HasValue)
            {
                Console.ResetColor();
            }
        }

        public static void WriteDebug(string debug, ConsoleColor? color = null)
        {
            if (!color.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
            }

            Console.Error.WriteLine(debug);
            Console.ResetColor();
        }

        public static void WriteToFile(string filePath, string contents)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                UTF8Encoding utf8WithoutBom = new UTF8Encoding(false);
                File.WriteAllText(filePath, contents, utf8WithoutBom);
            }
        }
    }
}
