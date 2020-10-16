using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Runtime.InteropServices;

namespace Azure.DigitalTwins.Resolver.Tests
{
    public class ClientTests
    {
        Mock<ILogger> _logger;

        [SetUp]
        public void Setup()
        {
            _logger = new Mock<ILogger>();
        }

        [Test]
        public void ClientInitGenericRepoUri()
        {
            string registryUriString = "https://localhost/myregistry/";
            Uri registryUri = new Uri(registryUriString);

            // Uses NullLogger
            ResolverClient client = new ResolverClient(registryUri);
            Assert.AreEqual(registryUri, client.RepositoryUri);

            client = new ResolverClient(registryUri, _logger.Object);
            Assert.AreEqual(registryUri, client.RepositoryUri);
            _logger.ValidateLog(StandardStrings.ClientInitWithFetcher(registryUri.Scheme), LogLevel.Information, Times.Once());
        }

        [Test]
        public void ClientInitRemoteRepoHelper()
        {
            string registryUriString = "https://localhost/myregistry/";
            Uri registryUri = new Uri(registryUriString);

            ResolverClient client = ResolverClient.FromRemoteRepository(registryUriString);
            Assert.AreEqual(registryUri, client.RepositoryUri);

            client = ResolverClient.FromRemoteRepository(registryUriString, _logger.Object);
            Assert.AreEqual(registryUri, client.RepositoryUri);
            _logger.ValidateLog(StandardStrings.ClientInitWithFetcher(registryUri.Scheme), LogLevel.Information, Times.Once());
        }

        [Test]
        public void ClientInitLocalRepoHelper()
        {
            string testModelRegistryPath = TestHelpers.GetTestLocalModelRepository();
            Uri registryUri = new Uri($"file://{testModelRegistryPath}");

            // Uses NullLogger
            ResolverClient client = ResolverClient.FromLocalRepository(testModelRegistryPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testModelRegistryPath = testModelRegistryPath.Replace("\\", "/");
            }

            Assert.AreEqual(registryUri, client.RepositoryUri);
            Assert.AreEqual(testModelRegistryPath, client.RepositoryUri.AbsolutePath);

            client = ResolverClient.FromLocalRepository(testModelRegistryPath, _logger.Object);
            Assert.AreEqual(registryUri, client.RepositoryUri);

            _logger.ValidateLog(StandardStrings.ClientInitWithFetcher(registryUri.Scheme), LogLevel.Information, Times.Once());
        }

        [TestCase("dtmi:com:example:Thermostat;1", true)]
        [TestCase("dtmi:contoso:scope:entity;2", true)]
        [TestCase("dtmi:com:example:Thermostat:1", false)]
        [TestCase("dtmi:com:example::Thermostat;1", false)]
        [TestCase("com:example:Thermostat;1", false)]
        public void ClientIsValidDtmi(string dtmi, bool expected)
        {
            Assert.AreEqual(ResolverClient.IsValidDtmi(dtmi), expected);
        }

        [TestCase("dtmi:com:example:Thermostat;1", "/dtmi/com/example/thermostat-1.json")]
        [TestCase("dtmi:com:example:Thermostat:1", null)]
        public void ClientLocalRepoGetPath(string dtmi, string expectedPath)
        {
            string testModelRegistryPath = TestHelpers.GetTestLocalModelRepository();
            ResolverClient client = ResolverClient.FromLocalRepository(testModelRegistryPath);

            if (string.IsNullOrEmpty(expectedPath))
            {
                ResolverException re = Assert.Throws<ResolverException>(() => client.GetPath(dtmi));
                Assert.AreEqual(re.Message, $"{StandardStrings.GenericResolverError(dtmi)}{StandardStrings.InvalidDtmiFormat(dtmi)}");
                return;
            }

            string modelPath = client.GetPath(dtmi);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                testModelRegistryPath = testModelRegistryPath.Replace("\\", "/");
            }

            Assert.AreEqual(modelPath, $"{testModelRegistryPath}{expectedPath}");
        }

        [TestCase("dtmi:com:example:Thermostat;1", "/dtmi/com/example/thermostat-1.json")]
        [TestCase("dtmi:com:example:Thermostat:1", null)]
        public void ClientRemoteRepoGetPath(string dtmi, string expectedPath)
        {
            string registryUriString = "https://localhost/myregistry";
            ResolverClient client = ResolverClient.FromRemoteRepository(registryUriString);

            if (string.IsNullOrEmpty(expectedPath))
            {
                ResolverException re = Assert.Throws<ResolverException>(() => client.GetPath(dtmi));
                Assert.AreEqual(re.Message, $"{StandardStrings.GenericResolverError(dtmi)}{StandardStrings.InvalidDtmiFormat(dtmi)}");
                return;
            }

            string modelPath = client.GetPath(dtmi);
            Assert.AreEqual(modelPath, $"{registryUriString}{expectedPath}");
        }

        [Test]
        public void ClientSettings()
        {
            DependencyResolutionOption defaultResolutionOption = DependencyResolutionOption.Enabled;
            ResolverClientSettings customSettings = 
                new ResolverClientSettings(DependencyResolutionOption.FromExpanded);

            string registryUriString = "https://localhost/myregistry/";
            Uri registryUri = new Uri(registryUriString);

            ResolverClient defaultClient = new ResolverClient(registryUri);
            Assert.AreEqual(defaultClient.Settings.DependencyResolution, defaultResolutionOption);

            ResolverClient customClient = new ResolverClient(registryUri, settings: customSettings);
            Assert.AreEqual(customClient.Settings.DependencyResolution, DependencyResolutionOption.FromExpanded);
        }
    }
}
