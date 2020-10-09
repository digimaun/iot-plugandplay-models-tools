using Azure.DigitalTwins.Resolver.Fetchers;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Azure.DigitalTwins.Resolver.Tests
{
    public class FetchIntegrationTests
    {
        readonly Uri _remoteUri = new Uri(TestHelpers.GetTestRemoteModelRepository());
        readonly Uri _localUri = new Uri($"file://{TestHelpers.GetTestLocalModelRepository()}");
        Mock<ILogger> _logger;
        IModelFetcher _localFetcher;
        IModelFetcher _remoteFetcher;

        [SetUp]
        public void Setup()
        {
            _logger = new Mock<ILogger>();
            _localFetcher = new LocalModelFetcher(_logger.Object);
            _remoteFetcher = new RemoteModelFetcher(_logger.Object);
        }

        [Test]
        public async Task FetchLocalRepository()
        {
            string targetDtmi = "dtmi:com:example:thermostat;1";

            string expectedPath = DtmiConventions.ToPath(targetDtmi, _localUri.AbsolutePath);
            string fetcherPath = _localFetcher.GetPath(targetDtmi, _localUri);
            Assert.AreEqual(fetcherPath, expectedPath);

            // Resolution correctness is covered in ResolverIntegrationTests
            FetchResult fetchResult = await _localFetcher.FetchAsync(targetDtmi, _localUri);
            Assert.IsNotNull(fetchResult.Definition);
 
            _logger.ValidateLog(StandardStrings.FetchingContent(fetcherPath), LogLevel.Information, Times.Once());
        }

        [Test]
        public void FetchLocalRepositoryDoesNotExist()
        {
            string targetDtmi = "dtmi:com:example:thermostat;1";

            Uri invalidFileUri = new Uri("file://totally/fake/path/please");
            Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await _localFetcher.FetchAsync(targetDtmi, invalidFileUri));

            _logger.ValidateLog(StandardStrings.ErrorAccessLocalRepository(invalidFileUri.AbsolutePath), LogLevel.Error, Times.Once());
        }

        [Test]
        public void FetchLocalRepositoryModelDoesNotExist()
        {
            string targetDtmi = "dtmi:com:example:thermojax;1";
            Assert.ThrowsAsync<FileNotFoundException>(async () => await _localFetcher.FetchAsync(targetDtmi, _localUri));

            string expectedModelPath = DtmiConventions.ToPath(targetDtmi, _localUri.AbsolutePath);
            _logger.ValidateLog(StandardStrings.ErrorAccessLocalRepositoryModel(expectedModelPath), LogLevel.Warning, Times.Once());
        }

        [Test]
        public async Task FetchRemoteRepository()
        {
            string targetDtmi = "dtmi:com:example:thermostat;1";

            string expectedPath = DtmiConventions.ToPath(targetDtmi, _remoteUri.AbsoluteUri);
            string fetcherPath = _remoteFetcher.GetPath(targetDtmi, _remoteUri);
            Assert.AreEqual(fetcherPath, expectedPath);

            // Resolution correctness is covered in ResolverIntegrationTests
            FetchResult fetchResult = await _remoteFetcher.FetchAsync(targetDtmi, _remoteUri);
            Assert.IsNotNull(fetchResult.Definition);

            _logger.ValidateLog($"{StandardStrings.FetchingContent(fetcherPath)}", LogLevel.Information, Times.Once());
        }

        [Test]
        public void FetchRemoteRepositoryDoesNotExist()
        {
            string targetDtmi = "dtmi:com:example:thermostat;1";
            Uri invalidRemoteUri = new Uri("http://localhost/fakeRepo/");
            Assert.ThrowsAsync<HttpRequestException>(async () => await _remoteFetcher.FetchAsync(targetDtmi, invalidRemoteUri));
        }

        [Test]
        public void FetchRemoteRepositoryModelDoesNotExist()
        {
            string targetDtmi = "dtmi:com:example:thermojax;1";
            Assert.ThrowsAsync<HttpRequestException>(async () => await _remoteFetcher.FetchAsync(targetDtmi, _remoteUri));
        }
    }
}