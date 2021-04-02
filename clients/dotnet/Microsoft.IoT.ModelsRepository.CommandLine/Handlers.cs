﻿using Azure;
using Azure.IoT.ModelsRepository;
using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Microsoft.IoT.ModelsRepository.CommandLine
{
    internal static class Handlers
    {
        // Alternative to enum to avoid casting.
        public static class ReturnCodes
        {
            public const int Success = 0;
            public const int InvalidArguments = 1;
            public const int ValidationError = 2;
            public const int ResolutionError = 3;
            public const int ProcessingError = 4;
        }

        public static async Task<int> Export(string dtmi, FileInfo modelFile, string repo, ModelDependencyResolution deps, string output)
        {
            //check that we have either model file or dtmi
            if (string.IsNullOrWhiteSpace(dtmi) && modelFile == null)
            {
                string invalidArgMsg = "Please specify a value for --dtmi";
                Outputs.WriteError(invalidArgMsg);
                return ReturnCodes.InvalidArguments;
            }

            Parsing parsing = new Parsing(repo);
            try
            {
                if (string.IsNullOrWhiteSpace(dtmi))
                {
                    dtmi = parsing.GetRootId(modelFile);
                    if (string.IsNullOrWhiteSpace(dtmi))
                    {
                        Outputs.WriteError("Model is missing root @id");
                        return ReturnCodes.ValidationError;
                    }
                }

                IDictionary<string, string> result = await parsing.GetRepositoryClient(dependencyResolution: deps).GetModelsAsync(dtmi);
                List<string> resultList = result.Values.ToList();
                string normalizedList = string.Join(',', resultList);
                string payload = $"[{normalizedList}]";

                using JsonDocument document = JsonDocument.Parse(payload, CommonOptions.DefaultJsonParseOptions);
                using MemoryStream stream = new MemoryStream();
                await JsonSerializer.SerializeAsync(stream, document.RootElement, CommonOptions.DefaultJsonSerializerOptions);
                stream.Position = 0;
                using StreamReader streamReader = new StreamReader(stream);
                string jsonSerialized = await streamReader.ReadToEndAsync();

                Outputs.WriteOut(jsonSerialized);
                Outputs.WriteToFile(output, jsonSerialized);
            }
            catch (RequestFailedException requestEx)
            {
                Outputs.WriteError(requestEx.Message);
                return ReturnCodes.ResolutionError;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Outputs.WriteError($"Parsing json-ld content. Details: {jsonEx.Message}");
                return ReturnCodes.InvalidArguments;
            }

            return ReturnCodes.Success;
        }

        public static async Task<int> Validate(FileInfo modelFile, string repo, ModelDependencyResolution deps, bool strict)
        {
            Parsing parsing = new Parsing(repo);

            try
            {
                FileExtractResult extractResult = parsing.ExtractModels(modelFile);
                List<string> models = extractResult.Models;

                if (models.Count == 0)
                {
                    Outputs.WriteError("No models to validate.");
                    return ReturnCodes.ValidationError;
                }

                ModelParser parser;
                if (models.Count > 1)
                {
                    // Special case: when validating from an array, only use array contents for resolution.
                    // Setup vanilla parser with no resolution. We get a better error message when a delegate is assigned.
                    parser = new ModelParser
                    {
                        DtmiResolver = (IReadOnlyCollection<Dtmi> dtmis) =>
                        {
                            return Task.FromResult(Enumerable.Empty<string>());
                        }
                    };
                }
                else
                {
                    parser = parsing.GetParser(dependencyResolution: deps);
                }

                Outputs.WriteOut($"- Validating models conform to DTDL...");
                await parser.ParseAsync(models);

                if (strict)
                {
                    if (extractResult.ContentKind == JsonValueKind.Array || models.Count > 1)
                    {
                        // Related to file path validation.
                        Outputs.WriteError("Strict validation requires a single root model object.");
                        return ReturnCodes.ValidationError;
                    }

                    string id = parsing.GetRootId(models[0]);
                    Outputs.WriteOut($"- Ensuring DTMIs namespace conformance for model \"{id}\"...");
                    List<string> invalidSubDtmis = Validations.EnsureSubDtmiNamespace(models[0]);
                    if (invalidSubDtmis.Count > 0)
                    {
                        Outputs.WriteError(
                            $"The following DTMI's do not start with the root DTMI namespace:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidSubDtmis)}");
                        return ReturnCodes.ValidationError;
                    }

                    // TODO: Evaluate changing how file path validation is invoked.
                    if (Validations.IsRemoteEndpoint(repo))
                    {
                        Outputs.WriteError($"Model file path validation requires a local repository.");
                        return ReturnCodes.ValidationError;
                    }

                    Outputs.WriteOut($"- Ensuring model file path adheres to DMR path conventions...");
                    string filePathError = Validations.EnsureValidModelFilePath(modelFile.FullName, models[0], repo);

                    if (filePathError != null)
                    {
                        Outputs.WriteError(
                            $"File \"{modelFile.FullName}\" does not adhere to DMR path conventions. Expecting \"{filePathError}\".");
                        return ReturnCodes.ValidationError;
                    }
                }
            }
            catch (ResolutionException resolutionEx)
            {
                Outputs.WriteError(resolutionEx.Message);
                return ReturnCodes.ResolutionError;
            }
            catch (RequestFailedException requestEx)
            {
                Outputs.WriteError(requestEx.Message);
                return ReturnCodes.ResolutionError;
            }
            catch (ParsingException parsingEx)
            {
                IList<ParsingError> errors = parsingEx.Errors;
                string normalizedErrors = string.Empty;
                foreach (ParsingError error in errors)
                {
                    normalizedErrors += $"{Environment.NewLine}{error.Message}";
                }

                Outputs.WriteError(normalizedErrors);
                return ReturnCodes.ValidationError;
            }
            catch (IOException ioEx)
            {
                Outputs.WriteError(ioEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (ArgumentException argEx)
            {
                Outputs.WriteError(argEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Outputs.WriteError($"Parsing json-ld content. Details: {jsonEx.Message}");
                return ReturnCodes.InvalidArguments;
            }

            return ReturnCodes.Success;
        }

        public static async Task<int> Import(FileInfo modelFile, DirectoryInfo localRepo, ModelDependencyResolution deps, bool strict)
        {
            if (localRepo == null)
            {
                localRepo = new DirectoryInfo(Path.GetFullPath("."));
            }

            Parsing parsing = new Parsing(localRepo.FullName);

            try
            {
                ModelParser parser = parsing.GetParser(dependencyResolution: deps);
                FileExtractResult extractResult = parsing.ExtractModels(modelFile);
                List<string> models = extractResult.Models;

                if (models.Count == 0)
                {
                    Outputs.WriteError("No models to import.");
                    return ReturnCodes.ValidationError;
                }

                Outputs.WriteOut($"- Validating models conform to DTDL...");
                await parser.ParseAsync(models);

                if (strict)
                {
                    foreach (string content in models)
                    {
                        string id = parsing.GetRootId(content);
                        Outputs.WriteOut($"- Ensuring DTMIs namespace conformance for model \"{id}\"...");
                        List<string> invalidSubDtmis = Validations.EnsureSubDtmiNamespace(content);
                        if (invalidSubDtmis.Count > 0)
                        {
                            Outputs.WriteError(
                                $"The following DTMI's do not start with the root DTMI namespace:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidSubDtmis)}");
                            return ReturnCodes.ValidationError;
                        }
                    }
                }

                foreach (string content in models)
                {
                    await ModelImporter.ImportAsync(content, localRepo);
                }
            }
            catch (ResolutionException resolutionEx)
            {
                Outputs.WriteError(resolutionEx.Message);
                return ReturnCodes.ResolutionError;
            }
            catch (RequestFailedException requestEx)
            {
                Outputs.WriteError(requestEx.Message);
                return ReturnCodes.ResolutionError;
            }
            catch (ParsingException parsingEx)
            {
                IList<ParsingError> errors = parsingEx.Errors;
                string normalizedErrors = string.Empty;
                foreach (ParsingError error in errors)
                {
                    normalizedErrors += $"{Environment.NewLine}{error.Message}";
                }

                Outputs.WriteError(normalizedErrors);
                return ReturnCodes.ValidationError;
            }
            catch (IOException ioEx)
            {
                Outputs.WriteError(ioEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (ArgumentException argEx)
            {
                Outputs.WriteError(argEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                Outputs.WriteError($"Parsing json-ld content. Details: {jsonEx.Message}");
                return ReturnCodes.InvalidArguments;
            }

            return ReturnCodes.Success;
        }

        public static int RepoIndex(DirectoryInfo localRepo, string output)
        {
            if (localRepo == null)
            {
                localRepo = new DirectoryInfo(Path.GetFullPath("."));
            }

            var parsing = new Parsing(null);
            var modelIndex = new ModelIndex();

            foreach (string file in Directory.EnumerateFiles(localRepo.FullName, "*.json",
                new EnumerationOptions { RecurseSubdirectories = true }))
            {
                if (file.ToLower().EndsWith("expanded.json")){
                    continue;
                }

                try
                {
                    var modelFile = new FileInfo(file);
                    ModelIndexEntry indexEntry = parsing.ParseModelFileForIndex(modelFile);
                    modelIndex.Add(indexEntry.Dtmi, indexEntry);
                }
                catch(Exception e)
                {
                    Outputs.WriteError($"Failure processing model file: {file}, {e.Message}");
                    return ReturnCodes.ProcessingError;
                }
            }

            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                IgnoreNullValues = true,
            };

            string indexJsonString = JsonSerializer.Serialize(modelIndex, serializeOptions);
            Outputs.WriteOut(indexJsonString);
            Outputs.WriteToFile(output, indexJsonString);

            return ReturnCodes.Success;
        }
    }
}
