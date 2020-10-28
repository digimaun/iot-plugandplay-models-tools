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
                Description = $"Microsoft IoT Plug and Play Device Models Repository CLI v{Outputs.CliVersion}"
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

        private static Command BuildExportCommand()
        {
            var modelFileOpt = CommonOptions.ModelFile;
            modelFileOpt.IsHidden = true;

            Command exportModelCommand = new Command("export")
            {
                CommonOptions.Dtmi,
                modelFileOpt,
                CommonOptions.Repo,
                CommonOptions.Deps,
                CommonOptions.Output,
                CommonOptions.Silent
            };

            exportModelCommand.Description =
                "Retrieve a model and its dependencies by dtmi or model file using the target repository for model resolution.";
            exportModelCommand.Handler = CommandHandler.Create<string, string, string, bool, FileInfo, DependencyResolutionOption, IHost>(
                async (dtmi, repo, output, silent, modelFile, deps, host) =>
            {
                ILogger logger = GetLogger(host);

                if (!silent)
                {
                    await Outputs.WriteHeadersAsync();
                    await Outputs.WriteInputsAsync("export",
                        new Dictionary<string, string> {
                            {"dtmi", dtmi },
                            {"model-file", modelFile?.FullName},
                            {"repo", repo },
                            {"deps", deps.ToString() },
                            {"output", output },
                        });
                }

                IDictionary<string, string> result;

                //check that we have either model file or dtmi
                if (string.IsNullOrWhiteSpace(dtmi) && modelFile == null)
                {
                    string invalidArgMsg = "Please specify a value for --dtmi!";
                    await Outputs.WriteErrorAsync(invalidArgMsg);
                    return ReturnCodes.InvalidArguments;
                }

                Parsing parsing = new Parsing(repo, logger);
                try
                {
                    if (string.IsNullOrWhiteSpace(dtmi))
                    {
                        dtmi = parsing.GetModelMetadata(modelFile).Id;
                        if (string.IsNullOrWhiteSpace(dtmi))
                        {
                            await Outputs.WriteErrorAsync("Model is missing root @id!");
                            return ReturnCodes.ParserError;
                        }
                    }

                    result = await parsing.GetResolver(resolutionOption: deps).ResolveAsync(dtmi);
                }
                catch (ResolverException resolverEx)
                {
                    await Outputs.WriteErrorAsync(resolverEx.Message);
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
                CommonOptions.Repo,
                CommonOptions.Deps,
                CommonOptions.Strict,
                CommonOptions.Silent
            };

            validateModelCommand.Description =
                "Validates a model using the DTDL model parser & resolver. The target repository is used for model resolution. ";
            validateModelCommand.Handler = CommandHandler.Create<FileInfo, string, IHost, bool, bool, DependencyResolutionOption>(
                async (modelFile, repo, host, silent, strict, deps) =>
            {
                ILogger logger = GetLogger(host);
                if (!silent)
                {
                    await Outputs.WriteHeadersAsync();
                    await Outputs.WriteInputsAsync("validate",
                        new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"repo", repo },
                            {"deps",  deps.ToString()},
                            {"strict", strict.ToString() }
                        });
                }

                string modelFileText = File.ReadAllText(modelFile.FullName);
                Parsing parsing = new Parsing(repo, logger);

                try
                {
                    Outputs.WriteOut("- Validating model conforms to DTDL...");
                    ModelParser parser = parsing.GetParser(resolutionOption: deps);
                    await parser.ParseAsync(new string[] { modelFileText });
                    Outputs.WriteOut($"Success{Environment.NewLine}");
                }
                catch (ResolutionException resolutionEx)
                {
                    await Outputs.WriteErrorAsync(resolutionEx.Message);
                    return ReturnCodes.ResolutionError;
                }
                catch (ResolverException resolverEx)
                {
                    await Outputs.WriteErrorAsync(resolverEx.Message);
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

                    await Outputs.WriteErrorAsync(normalizedErrors);
                    return ReturnCodes.ParserError;
                }

                if (strict)
                {
                    // TODO silent?
                    Outputs.WriteOut("- Validating file path...");
                    if (!Validations.ValidateFilePath(modelFile.FullName))
                    {
                        await Outputs.WriteErrorAsync($"File \"{modelFile.FullName}\" does not adhere to DMR naming conventions!");
                        return ReturnCodes.ValidationError;
                    }
                    Outputs.WriteOut($"Success{Environment.NewLine}");

                    Outputs.WriteOut("- Scanning DTMI's for reserved words...");
                    List<string> invalidReservedIds = Validations.ScanIdsForReservedWords(modelFileText);
                    if (invalidReservedIds.Count > 0)
                    {
                        await Outputs.WriteErrorAsync(
                            $"Reserved words found in the following DTMI's:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidReservedIds)}");
                        return ReturnCodes.ValidationError;
                    }
                    Outputs.WriteOut($"Success{Environment.NewLine}");

                    Outputs.WriteOut("- Ensuring DTMI's namespace conformance...");
                    List<string> invalidSubDtmis = Validations.EnsureSubDtmiNamespace(modelFileText);
                    if (invalidSubDtmis.Count > 0)
                    {
                        await Outputs.WriteErrorAsync(
                            $"The following DTMI's do not start with the root DTMI namespace:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidReservedIds)}");
                        return ReturnCodes.ValidationError;
                    }
                    Outputs.WriteOut($"Success{Environment.NewLine}");
                }

                return ReturnCodes.Success;
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
            addModel.IsHidden = true;
            addModel.Description = "Adds a model to a local repository. " +
                "Validates Id's, dependencies and places model content in the proper location.";
            addModel.Handler = CommandHandler.Create<FileInfo, DirectoryInfo, bool, IHost>(
                async (modelFile, localRepo, silent, host) =>
            {
                var returnCode = ReturnCodes.Success;
                ILogger logger = GetLogger(host);
                if (!silent)
                {
                    await Outputs.WriteHeadersAsync();
                    await Outputs.WriteInputsAsync("import",
                        new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"local-repo", localRepo.FullName },
                        });
                }

                if (localRepo == null)
                {
                    localRepo = new DirectoryInfo(Path.GetFullPath("."));
                }

                Parsing parsing = new Parsing(localRepo.FullName, logger);
                try
                {
                    /*
                    var newModels = await ModelImporter.ImportModels(modelFile, localRepo, logger);
                    foreach (var model in newModels)
                    {
                        var validationResult = await parsing.IsValidDtdlFileAsync(model, false);

                        if (!validationResult)
                            returnCode = ReturnCodes.ValidationError;
                    }
                    */
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
