using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Azure.IoT.DeviceModelsRepository.Resolver
{
    public class ResolverClient
    {
        readonly RepositoryHandler repositoryHandler = null;

        public static ResolverClient FromLocalRepository(string repositoryPath, ResolverClientOptions options = null, ILogger logger = null)
        {
            repositoryPath = Path.GetFullPath(repositoryPath);
            return new ResolverClient(new Uri($"file://{repositoryPath}"), options, logger);
        }

        public ResolverClient() : this(new Uri(""), null, null) { }

        public ResolverClient(Uri repositoryUri): this(repositoryUri, null, null) { }

        public ResolverClient(Uri repositoryUri, ResolverClientOptions options): this(repositoryUri, options, null) { }

        public ResolverClient(Uri repositoryUri, ResolverClientOptions options = null, ILogger logger = null)
        {
            this.repositoryHandler = new RepositoryHandler(repositoryUri, options, logger);
        }

        public virtual async Task<IDictionary<string, string>> ResolveAsync(string dtmi)
        {
            return await this.repositoryHandler.ProcessAsync(dtmi);
        }

        public virtual async Task<IDictionary<string, string>> ResolveAsync(params string[] dtmis)
        {
            return await this.repositoryHandler.ProcessAsync(dtmis);
        }

        public virtual async Task<IDictionary<string, string>> ResolveAsync(IEnumerable<string> dtmis)
        {
            return await this.repositoryHandler.ProcessAsync(dtmis);
        }

        public virtual string GetPath(string dtmi) => repositoryHandler.ToPath(dtmi);

        public static bool IsValidDtmi(string dtmi) => DtmiConventions.IsDtmi(dtmi);

        public Uri RepositoryUri  => repositoryHandler.RepositoryUri;

        public ResolverClientOptions Settings => repositoryHandler.Settings;
    }
}
