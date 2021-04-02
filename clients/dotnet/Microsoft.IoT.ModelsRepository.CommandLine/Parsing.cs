using Azure.IoT.ModelsRepository;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.IoT.ModelsRepository.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Microsoft.IoT.ModelsRepository.CommandLine
{
    internal class Parsing
    {
        private readonly string _repository;

        public Parsing(string repository)
        {
            _repository = repository;
        }

        public ModelParser GetParser(ModelDependencyResolution dependencyResolution = ModelDependencyResolution.Enabled)
        {
            ModelsRepositoryClient client = GetRepositoryClient(dependencyResolution);
            ModelParser parser = new ModelParser
            {
                DtmiResolver = client.ParserDtmiResolver
            };
            return parser;
        }

        public ModelsRepositoryClient GetRepositoryClient(ModelDependencyResolution dependencyResolution = ModelDependencyResolution.Enabled)
        {
            string repository = _repository;
            if (Validations.IsRelativePath(repository))
            {
                repository = Path.GetFullPath(repository);
            }

            return new ModelsRepositoryClient(
                new Uri(repository),
                new ModelsRepositoryClientOptions(dependencyResolution: dependencyResolution));
        }

        public FileExtractResult ExtractModels(FileInfo modelsFile)
        {
            string modelsText = File.ReadAllText(modelsFile.FullName);
            return ExtractModels(modelsText);
        }

        public FileExtractResult ExtractModels(string modelsText)
        {
            List<string> result = new List<string>();
            using JsonDocument document = JsonDocument.Parse(modelsText);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                result.Add(root.GetRawText());
                return new FileExtractResult(result, root.ValueKind);
            }
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    result.Add(element.GetRawText());
                }
                return new FileExtractResult(result, root.ValueKind);
            }

            throw new ArgumentException($"Importing model file contents of kind {root.ValueKind} is not yet supported.");
        }

        public string GetRootId(string modelText)
        {
            using JsonDocument document = JsonDocument.Parse(modelText);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("@id", out JsonElement id))
            {
                if (id.ValueKind == JsonValueKind.String)
                {
                    return id.GetString();
                }
            }

            return string.Empty;
        }

        public string GetRootId(FileInfo fileInfo)
        {
            return GetRootId(File.ReadAllText(fileInfo.FullName));
        }

        public ModelIndexEntry ParseModelFileForIndex(FileInfo fileInfo)
        {
            string modelText = File.ReadAllText(fileInfo.FullName);

            using JsonDocument document = JsonDocument.Parse(modelText);
            JsonElement root = document.RootElement;

            object description = null;
            object displayName = null;

            if (root.TryGetProperty("description", out JsonElement descriptionElement))
            {
                description = JsonSerializer.Deserialize<object>(descriptionElement.GetRawText());
            }

            if (root.TryGetProperty("displayName", out JsonElement displaneNameElement))
            {
                displayName = JsonSerializer.Deserialize<object>(displaneNameElement.GetRawText());
            }

            var indexEntry = new ModelIndexEntry()
            {
                Dtmi = root.GetProperty("@id").GetString(),
                Description = description,
                DisplayName = displayName
            };

            return indexEntry;
        }
    }
}
