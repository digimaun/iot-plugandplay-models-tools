﻿using Azure.Core.Diagnostics;
using Azure.IoT.ModelsRepository;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.IoT.ModelsRepository.CommandLine
{
    class Program
    {
        static async Task<int> Main(string[] args) => await GetCommandLine().UseDefaults().Build().InvokeAsync(args);

        private static CommandLineBuilder GetCommandLine()
        {
            RootCommand root = new RootCommand("parent")
            {
                Description = $"Microsoft IoT Models Repository CommandLine v{Outputs.CliVersion}"
            };

            root.Add(BuildExportCommand());
            root.Add(BuildValidateCommand());
            root.Add(BuildImportModelCommand());
            root.Add(BuildRepoCommandSet());

            root.AddGlobalOption(CommonOptions.Debug);
            root.AddGlobalOption(CommonOptions.Silent);

            CommandLineBuilder builder = new CommandLineBuilder(root);
            builder.UseMiddleware(async (context, next) =>
            {
                AzureEventSourceListener listener = null;
                try
                {
                    if (context.ParseResult.Tokens.Any(x => x.Type == TokenType.Option && x.Value == "--debug"))
                    {
                        Outputs.WriteHeader();
                        listener = AzureEventSourceListener.CreateConsoleLogger(EventLevel.Verbose);
                        Outputs.WriteDebug(context.ParseResult.ToString());
                    }

                    if (context.ParseResult.Tokens.Any(x => x.Type == TokenType.Option && x.Value == "--silent"))
                    {
                        System.Console.SetOut(TextWriter.Null);
                    }

                    await next(context);
                }
                finally
                {
                    if (listener != null)
                    {
                        listener.Dispose();
                    }
                }
            });

            return builder;
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
                CommonOptions.Output
            };

            exportModelCommand.Description =
                "Retrieve a model and its dependencies by dtmi using the target repository for model resolution.";
            exportModelCommand.Handler = CommandHandler.Create<string, FileInfo, string, ModelDependencyResolution, string>(Handlers.Export);

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
            };

            validateModelCommand.Description =
                "Validates the DTDL model contained in a file. When validating a single model object " +
                "the target repository is used for model resolution. When validating an array of models only the array " +
                "contents is used for resolution.";

            validateModelCommand.Handler =
                CommandHandler.Create<FileInfo, string, ModelDependencyResolution, bool>(Handlers.Validate);

            return validateModelCommand;
        }

        private static Command BuildImportModelCommand()
        {
            var modelFileOption = CommonOptions.ModelFile;
            modelFileOption.IsRequired = true; // Option is required for this command
            var depsResOption = CommonOptions.Deps;
            depsResOption.IsHidden = true; // Option has limited value for this command

            Command importModelCommand = new Command("import")
            {
                modelFileOption,
                CommonOptions.LocalRepo,
                depsResOption,
                CommonOptions.Strict,
            };
            importModelCommand.Description =
                "Imports models from a model file into the local repository. The local repository is used for model resolution.";
            importModelCommand.Handler = CommandHandler.Create<FileInfo, DirectoryInfo, ModelDependencyResolution, bool>(Handlers.Import);

            return importModelCommand;
        }

        private static Command BuildRepoCommandSet()
        {
            Command repoCommandRoot = new Command("repo")
            {
                Description = "Command group with operations that support managing a models repository.",
            };

            repoCommandRoot.Add(BuildRepoIndexCommand());

            return repoCommandRoot;
        }

        private static Command BuildRepoIndexCommand()
        {
            Command repoIndexCommand= new Command("index")
            {
                CommonOptions.LocalRepo,
                CommonOptions.Output
            };
            repoIndexCommand.Description =
                "Produce an index file from the state of a target local models repository.";
            repoIndexCommand.Handler = CommandHandler.Create<DirectoryInfo, string>(Handlers.RepoIndex);

            return repoIndexCommand;
        }
    }
}
