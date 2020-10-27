using Azure.IoT.DeviceModelsRepository.Resolver;
using Azure.IoT.DeviceModelsRepository.Resolver.Extensions;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Azure.IoT.DeviceModelsRepository.CLI
{
    public class Parsing
    {
        private readonly ILogger _logger;
        private readonly string _repository;

        public Parsing(string repository, ILogger logger)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<bool> IsValidDtdlFileAsync(FileInfo modelFile, bool strict, 
            DependencyResolutionOption resolutionOption = DependencyResolutionOption.Enabled)
        {
            _logger.LogTrace($"Using repository: '{_repository}'");
            ModelParser parser = GetParser(resolutionOption);

            await parser.ParseAsync(new string[] { File.ReadAllText(modelFile.FullName) });
            if (strict)
            {
                return await modelFile.Validate();
            }

            return true;
        }

        public ModelParser GetParser(DependencyResolutionOption resolutionOption = DependencyResolutionOption.Enabled)
        {
            ResolverClient client = GetResolver(resolutionOption);
            ModelParser parser = new ModelParser
            {
                DtmiResolver = client.ParserDtmiResolver
            };
            return parser;
        }

        public ResolverClient GetResolver(DependencyResolutionOption resolutionOption = DependencyResolutionOption.Enabled)
        {
            string repository = _repository;
            if (Validations.IsRelativePath(repository))
            {
                repository = Path.GetFullPath(repository);
            }

            return new ResolverClient(
                repository,
                new ResolverClientOptions(resolutionOption),
                _logger);
        }

        public ModelMetadata GetModelMetadata(FileInfo fileName)
        {
            ModelQuery modelQuery = new ModelQuery(File.ReadAllText(fileName.FullName));
            return modelQuery.GetMetadata();
        }
    }
}
