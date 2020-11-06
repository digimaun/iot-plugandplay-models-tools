﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Azure.IoT.DeviceModelsRepository.CLI.Tests
{
    public class ExportIntegrationTests
    {
        [TestCase(
            "dtmi:com:example:Thermostat;1",
            "",
            TestHelpers.ClientType.Remote,
            "")]
        [TestCase(
            "dtmi:com:example:Thermostat;1",
            "",
            TestHelpers.ClientType.Local,
            "Disabled")]
        [TestCase(
            "dtmi:com:example:Thermostat;1",
            "",
            TestHelpers.ClientType.Local,
            "TryFromExpanded")]
        [TestCase(
            "dtmi:com:example:TemperatureController;1",
            "dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1",
            TestHelpers.ClientType.Remote,
            "")]
        [TestCase(
            "dtmi:com:example:TemperatureController;1",
            "",
            TestHelpers.ClientType.Remote,
            "Disabled")]
        [TestCase(
            "dtmi:com:example:TemperatureController;1",
            "dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1",
            TestHelpers.ClientType.Local,
            "TryFromExpanded")]
        public void ExportInvocation(string dtmi, string expectedDeps, TestHelpers.ClientType clientType, string resolution)
        {
            string targetRepo = string.Empty;
            if (clientType == TestHelpers.ClientType.Local)
            {
                targetRepo = $"--repo \"{TestHelpers.TestLocalModelRepository}\"";
            }

            if (resolution != "")
            {
                resolution = $"--deps {resolution}";
            }

            (int returnCode, string standardOut, string standardError) = 
                ClientInvokator.Invoke($"export --dtmi \"{dtmi}\" {targetRepo} {resolution}");

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.True(!standardError.Contains("ERROR:"));
            Assert.True(standardError.Contains(Outputs.StandardHeader));

            Parsing parsing = new Parsing(null);
            List<string> modelsResult = parsing.ExtractModels(standardOut);

            string[] expectedDtmis = $"{dtmi},{expectedDeps}".Split(",", StringSplitOptions.RemoveEmptyEntries);
            Assert.True(modelsResult.Count == expectedDtmis.Length);
            
            foreach (string model in modelsResult)
            {
                string targetId = parsing.GetRootId(model);
                Assert.True(expectedDtmis.Contains(targetId));
            }
        }

        [TestCase("dtmi:com:example:Thermostat;1", "./dmr-export.json")]
        public void ExportOutFile(string dtmi, string outfilePath)
        {
            string qualifiedPath = Path.GetFullPath(outfilePath);
            (int returnCode, _, string standardError) =
                ClientInvokator.Invoke($"export -o \"{qualifiedPath}\" --dtmi \"{dtmi}\" --repo \"{TestHelpers.TestLocalModelRepository}\"");
            
            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.True(!standardError.Contains("ERROR:"));
            Parsing parsing = new Parsing(null);
            List<string> modelsResult = parsing.ExtractModels(new FileInfo(qualifiedPath));
            string targetId = parsing.GetRootId(modelsResult[0]);
            Assert.AreEqual(dtmi, targetId);
        }

        [TestCase("dtmi:com:example:Thermostat;")]
        [TestCase("")]
        public void ExportInvalidDtmiFormatWillError(string dtmi)
        {
            (int returnCode, _, string standardError) = ClientInvokator.Invoke($"export --dtmi \"{dtmi}\"");

            Assert.AreEqual(Handlers.ReturnCodes.InvalidArguments, returnCode);
            Assert.True(standardError.StartsWith("Invalid dtmi format"));
        }

        [TestCase("dtmi:com:example:Thermojax;999", TestHelpers.ClientType.Local)]
        [TestCase("dtmi:com:example:Thermojax;999", TestHelpers.ClientType.Remote)]
        public void ExportNonExistantDtmiWillError(string dtmi, TestHelpers.ClientType clientType)
        {
            string targetRepo = string.Empty;
            if (clientType == TestHelpers.ClientType.Local)
            {
                targetRepo = $"--repo \"{TestHelpers.TestLocalModelRepository}\"";
            }

            (int returnCode, _, string standardError) =
                ClientInvokator.Invoke($"export --dtmi \"{dtmi}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.ResolutionError, returnCode);
            Assert.True(standardError.Contains("ERROR:"));
        }
    }
}
