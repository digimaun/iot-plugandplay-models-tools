using Azure.IoT.DeviceModelsRepository.Resolver;
using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace Azure.IoT.DeviceModelsRepository.CLI
{
    internal static class Handlers
    {
        // Alternative to enum to avoid casting.
        static class ReturnCodes
        {
            public const int Success = 0;
            public const int InvalidArguments = 1;
            public const int ParserError = 2;
            public const int ResolutionError = 3;
            public const int ValidationError = 4;
        }

        public static async Task<int> Export(string dtmi, string repo, string output, bool silent, FileInfo modelFile, DependencyResolutionOption deps)
        {
            if (!silent)
            {
                await Outputs.WriteHeaderAsync();
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
                string invalidArgMsg = "Please specify a value for --dtmi";
                await Outputs.WriteErrorAsync(invalidArgMsg);
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
                        await Outputs.WriteErrorAsync("Model is missing root @id");
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
                await Outputs.WriteOutAsync(jsonSerialized);

            if (!string.IsNullOrEmpty(output))
            {
                UTF8Encoding utf8WithoutBom = new UTF8Encoding(false);
                await File.WriteAllTextAsync(output, jsonSerialized, utf8WithoutBom);
            }

            return ReturnCodes.Success;
        }

        public static async Task<int> Validate(FileInfo modelFile, string repo, bool silent, bool strict, DependencyResolutionOption deps)
        {
            if (!silent)
            {
                await Outputs.WriteHeaderAsync();
                await Outputs.WriteInputsAsync("validate",
                    new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"repo", repo },
                            {"deps",  deps.ToString()},
                            {"strict", strict.ToString() }
                    });
            }

            Parsing parsing = new Parsing(repo);

            try
            {
                ModelParser parser = parsing.GetParser(resolutionOption: deps);
                List<string> models = parsing.ExtractModels(modelFile);

                await Outputs.WriteOutAsync($"- Validating models conform to DTDL...");
                await parser.ParseAsync(models);

                if (strict)
                {
                    foreach (string content in models)
                    {
                        string id = parsing.GetRootId(content);
                        await Outputs.WriteOutAsync($"- Ensuring DTMIs namespace conformance for model \"{id}\"...");
                        List<string> invalidSubDtmis = Validations.EnsureSubDtmiNamespace(content);
                        if (invalidSubDtmis.Count > 0)
                        {
                            await Outputs.WriteErrorAsync(
                                $"The following DTMI's do not start with the root DTMI namespace:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidSubDtmis)}");
                            return ReturnCodes.ValidationError;
                        }
                    }

                    await Outputs.WriteOutAsync($"- Ensuring model file path adheres to DMR path conventions...");
                    if (!Validations.IsValidDtmiPath(modelFile.FullName))
                    {
                        await Outputs.WriteErrorAsync($"File \"{modelFile.FullName}\" does not adhere to DMR path conventions.");
                        return ReturnCodes.ValidationError;
                    }
                }
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
                    normalizedErrors += $"{Environment.NewLine}{error.Message}";
                }

                await Outputs.WriteErrorAsync(normalizedErrors);
                return ReturnCodes.ParserError;
            }
            catch (IOException ioEx)
            {
                await Outputs.WriteErrorAsync(ioEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (ArgumentException argEx)
            {
                await Outputs.WriteErrorAsync(argEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                await Outputs.WriteErrorAsync(jsonEx.Message);
                return ReturnCodes.InvalidArguments;
            }

            return ReturnCodes.Success;
        }

        public static async Task<int> Import(FileInfo modelFile, DirectoryInfo localRepo, DependencyResolutionOption deps, bool silent, bool strict)
        {
            if (localRepo == null)
            {
                localRepo = new DirectoryInfo(Path.GetFullPath("."));
            }

            if (!silent)
            {
                await Outputs.WriteHeaderAsync();
                await Outputs.WriteInputsAsync("import",
                    new Dictionary<string, string> {
                            {"model-file", modelFile.FullName },
                            {"local-repo", localRepo.FullName },
                            {"deps",  deps.ToString()},
                            {"strict", strict.ToString()}
                    });
            }

            Parsing parsing = new Parsing(localRepo.FullName);

            try
            {
                ModelParser parser = parsing.GetParser(resolutionOption: deps);
                List<string> models = parsing.ExtractModels(modelFile);

                await Outputs.WriteOutAsync($"- Validating models conform to DTDL...");
                await parser.ParseAsync(models);

                if (strict)
                {
                    foreach (string content in models)
                    {
                        string id = parsing.GetRootId(content);
                        await Outputs.WriteOutAsync($"- Ensuring DTMIs namespace conformance for model \"{id}\"...");
                        List<string> invalidSubDtmis = Validations.EnsureSubDtmiNamespace(content);
                        if (invalidSubDtmis.Count > 0)
                        {
                            await Outputs.WriteErrorAsync(
                                $"The following DTMI's do not start with the root DTMI namespace:{Environment.NewLine}{string.Join($",{Environment.NewLine}", invalidSubDtmis)}");
                            return ReturnCodes.ValidationError;
                        }
                    }
                }

                foreach (string content in models)
                {
                    ModelImporter.Import(content, localRepo);
                }
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
                    normalizedErrors += $"{Environment.NewLine}{error.Message}";
                }

                await Outputs.WriteErrorAsync(normalizedErrors);
                return ReturnCodes.ParserError;
            }
            catch (IOException ioEx)
            {
                await Outputs.WriteErrorAsync(ioEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (ArgumentException argEx)
            {
                await Outputs.WriteErrorAsync(argEx.Message);
                return ReturnCodes.InvalidArguments;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                await Outputs.WriteErrorAsync(jsonEx.Message);
                return ReturnCodes.InvalidArguments;
            }

            return ReturnCodes.Success;
        }
    }
}
