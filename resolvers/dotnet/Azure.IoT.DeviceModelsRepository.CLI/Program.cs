using Azure.IoT.DeviceModelsRepository.CLI.Exceptions;
using Azure.IoT.DeviceModelsRepository.Resolver;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.IoT.DeviceModelsRepository.CLI
{
    class Program
    {
        private static readonly string _parserVersion = typeof(ModelParser).Assembly.GetName().Version.ToString();
        private static readonly string _resolverVersion = typeof(ResolverClient).Assembly.GetName().Version.ToString();
        private static readonly string _cliVersion = typeof(Program).Assembly.GetName().Version.ToString();

        // Alternative to enum to avoid casting.
        public static class ReturnCodes
        {
            public const int Success = 0;
            public const int InvalidArguments = 1;
            public const int ParserError = 2;
            public const int ResolutionError = 3;
            public const int ValidationError = 4;
            public const int ImportError = 5;
        }

        static async Task<int> Main(string[] args) => await BuildCommandLine()
            .UseHost(_ => Host.CreateDefaultBuilder())
            .UseDefaults()
            .Build()
            .InvokeAsync(args);

        private static CommandLineBuilder BuildCommandLine()
        {
            RootCommand root = new RootCommand("parent")
            {
                Description = "Microsoft IoT Plug and Play Device Model Repository CLI"
            };

            root.Add(BuildExportCommand());
            root.Add(BuildValidateCommand());
            root.Add(BuildImportModelCommand());

            return new CommandLineBuilder(root);
        }

        private static ILogger GetLogger(IHost host)
        {
            IServiceProvider serviceProvider = host.Services;
            ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            return loggerFactory.CreateLogger(typeof(Program));
        }

        private async static Task PrintHeaders()
        {
            await Console.Out.WriteLineAsync($"dmr-client/{_cliVersion} parser/{_parserVersion} resolver/{_resolverVersion}");
            await Console.Out.WriteLineAsync(Environment.NewLine);
        }

        private async static Task PrintInput(string command, Dictionary<string, string> inputs)
        {
            await Console.Out.WriteLineAsync($"Executing {command}");
            foreach (var item in inputs)
            {
                await Console.Out.WriteLineAsync($"{item.Key}={item.Value}");
            }
            await Console.Out.WriteLineAsync(Environment.NewLine);
        }

        private static Command BuildExportCommand()
        {
            Command exportModelCommand = new Command("export")
            {
                CommonOptions.Dtmi,
                CommonOptions.Repository,
                CommonOptions.Output,
                CommonOptions.Silent,
                CommonOptions.ModelFile
            };

            exportModelCommand.Description =
                "Retrieve a model and its dependencies by dtmi or model file using the target repository for model resolution.";
            exportModelCommand.Handler = CommandHandler.Create<string, string, IHost, string, bool, FileInfo>(
                async (dtmi, repository, host, output, silent, modelFile) =>
            {
                ILogger logger = GetLogger(host);

                if (!silent)
                {
                    await PrintHeaders();
                    await PrintInput("export",
                        new Dictionary<string, string> {
                            {"dtmi", dtmi },
                            {"repository", repository },
                            {"output", output }
                        });
                }

                IDictionary<string, string> result;

                //check that we have either model file or dtmi
                if (string.IsNullOrWhiteSpace(dtmi) && modelFile == null)
                {
                    string invalidArgMsg = "Please specify a value for --dtmi OR --model-file!";
                    logger.LogError(invalidArgMsg);
                    await Console.Error.WriteLineAsync(invalidArgMsg);
                    return ReturnCodes.InvalidArguments;
                }

                Parsing parsing = new Parsing(repository, logger);
                try
                {
                    if (string.IsNullOrWhiteSpace(dtmi))
                    {
                        dtmi = parsing.GetModelMetadata(modelFile).Id;
                        if (string.IsNullOrWhiteSpace(dtmi))
                        {
                            await Console.Error.WriteLineAsync("Model is missing root @id!");
                            return ReturnCodes.ParserError;
                        }
                    }

                    result = await parsing.GetResolver().ResolveAsync(dtmi);

                }
                catch (ResolverException resolverEx)
                {
                    logger.LogError(resolverEx.Message);
                    await Console.Error.WriteLineAsync(resolverEx.Message);
                    return ReturnCodes.ResolutionError;
                }

                List<string> resultList = result.Values.ToList();
                string normalizedList = string.Join(',', resultList);
                string payload = $"[{normalizedList}]";

                using JsonDocument document = JsonDocument.Parse(payload, CommonOptions.DefaultJsonParseOptions);
                using MemoryStream stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, document.RootElement, CommonOptions.DefaultJsonSerializerOptions);
                stream.Position = 0;
                using StreamReader streamReader = new StreamReader(stream);
                string jsonSerialized = await streamReader.ReadToEndAsync();

                if (!silent)
                    await Console.Out.WriteLineAsync(jsonSerialized);

                if (!string.IsNullOrEmpty(output))
                {
                    logger.LogTrace($"Writing result to file '{output}'");
                    await File.WriteAllTextAsync(output, jsonSerialized, Encoding.UTF8);
                }

                return ReturnCodes.Success;
            });

            return exportModelCommand;
        }

        private static Command BuildValidateCommand()
        {
            var modelFileOption = CommonOptions.ModelFile;
            modelFileOption.IsRequired = true; // Option is required for this command

            Command validateModelCommand = new Command("validate")
            {
                modelFileOption,
                CommonOptions.Repository,
                CommonOptions.Strict
            };

            validateModelCommand.Description = 
                "Validates a model using the DTDL model parser & resolver. The target repository is used for model resolution. ";
            validateModelCommand.Handler = CommandHandler.Create<FileInfo, string, IHost, bool, bool>(
                async (modelFile, repository, host, silent, strict) =>
            {
                ILogger logger = GetLogger(host);
                if (!silent)
                {
                    await PrintHeaders();
                    await PrintInput("validate",
                        new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"repository", repository },
                            {"strict", strict.ToString() }
                        });
                }

                Parsing parsing = new Parsing(repository, logger);
                bool isValid;
                try
                {
                    isValid = await parsing.IsValidDtdlFileAsync(modelFile, strict);
                }
                catch (ResolutionException resolutionEx)
                {
                    logger.LogError(resolutionEx.Message);
                    await Console.Error.WriteLineAsync(resolutionEx.Message);
                    return ReturnCodes.ResolutionError;
                }
                catch (ParsingException parsingEx)
                {
                    IList<ParsingError> errors = parsingEx.Errors;
                    string normalizedErrors = string.Empty;
                    foreach (ParsingError error in errors)
                    {
                        normalizedErrors += $"{error.Message}{Environment.NewLine}";
                    }

                    await Console.Error.WriteLineAsync(normalizedErrors);
                    return ReturnCodes.ParserError;
                }
                catch (ResolverException resolverEx)
                {
                    await Console.Error.WriteLineAsync(resolverEx.Message);
                    return ReturnCodes.ResolutionError;
                }
                catch (ValidationException validationEx)
                {
                    await Console.Error.WriteLineAsync(validationEx.Message);
                    return ReturnCodes.ValidationError;
                }

                return isValid ? ReturnCodes.Success : ReturnCodes.ValidationError;
            });

            return validateModelCommand;
        }

        private static Command BuildImportModelCommand()
        {
            var modelFileOption = CommonOptions.ModelFile;
            modelFileOption.IsRequired = true; // Option is required for this command

            Command addModel = new Command("import")
            {
                modelFileOption,
                CommonOptions.LocalRepo,
                CommonOptions.Silent
            };
            addModel.Description = "Adds a model to a local repository. " +
                "Validates Id's, dependencies and places model content in the proper location.";
            addModel.Handler = CommandHandler.Create<FileInfo, DirectoryInfo, bool, IHost>(
                async (modelFile, localRepository, silent, host) =>
            {
                var returnCode = ReturnCodes.Success;
                ILogger logger = GetLogger(host);
                if (!silent)
                {
                    await PrintHeaders();
                    await PrintInput("import",
                        new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"repository", localRepository.FullName },
                        });
                }

                if (localRepository == null)
                {
                    localRepository = new DirectoryInfo(Path.GetFullPath("."));
                }

                Parsing parsing = new Parsing(localRepository.FullName, logger);
                try
                {
                    var newModels = await ModelImporter.ImportModels(modelFile, localRepository, logger);
                    foreach (var model in newModels)
                    {
                        var validationResult = await parsing.IsValidDtdlFileAsync(model, false);

                        if (!validationResult)
                            returnCode = ReturnCodes.ValidationError;
                    }
                }
                catch (ResolverException resolverEx)
                {
                    await Console.Error.WriteLineAsync(resolverEx.Message);
                    return ReturnCodes.ResolutionError;
                }
                catch (ParsingException parsingEx)
                {
                    IList<ParsingError> errors = parsingEx.Errors;
                    string normalizedErrors = string.Empty;
                    foreach (ParsingError error in errors)
                    {
                        normalizedErrors += $"{error.Message}{Environment.NewLine}";
                    }

                    await Console.Error.WriteLineAsync(normalizedErrors);
                    return ReturnCodes.ParserError;
                }
                catch (ValidationException validationEx)
                {
                    await Console.Error.WriteLineAsync(validationEx.Message);
                    return ReturnCodes.ValidationError;
                }
                catch (IOException ioEx)
                {
                    await Console.Error.WriteLineAsync(ioEx.Message);
                    return ReturnCodes.ImportError;
                }

                return returnCode;
            });

            return addModel;
        }
    }
}
