﻿using NUnit.Framework;
using System;
using System.IO;

namespace Microsoft.IoT.ModelsRepository.CommandLine.Tests
{
    [NonParallelizable]
    public class CommandImportIntegrationTests
    {
        readonly string testDirectory = TestContext.CurrentContext.TestDirectory;
        DirectoryInfo testImportRepo;

        [OneTimeSetUp]
        public void ResetTestRepoDir()
        {
            testImportRepo = new DirectoryInfo(Path.Combine(testDirectory, "MyModelRepo"));
            if (testImportRepo.Exists && testImportRepo.Name == "MyModelRepo")
            {
                testImportRepo.Delete(true);
            }
        }

        // TODO: Cheap ordering.
        [TestCase("1", "dtmi/com/example/thermostat-1.json", "dtmi:com:example:Thermostat;1", true)]
        [TestCase("2", "dtmi/azure/devicemanagement/deviceinformation-1.json", "dtmi:azure:DeviceManagement:DeviceInformation;1", false)]
        [TestCase("3", "dtmi/com/example/temperaturecontroller-1.json", "dtmi:com:example:TemperatureController;1", true)]
        public void ImportModelFileSingleModelObject(string _, string modelFilePath, string expectedDtmi, bool strict)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string strictSwitch = strict ? "--strict" : "";

            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo} {strictSwitch}");

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.False(standardError.Contains("Error:"));

            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
            Assert.True(standardOut.Contains($"- Importing model \"{expectedDtmi}\"..."));

            FileInfo modelFile = new FileInfo(Path.GetFullPath(testImportRepo.FullName + "/" + modelFilePath));
            Assert.True(modelFile.Exists);
            DateTime lastWriteTimeUtc = modelFile.LastWriteTimeUtc;
            Assert.AreEqual(expectedDtmi, ParsingUtils.GetRootId(modelFile));

            if (strict)
            {
                Assert.True(standardOut.Contains("- Ensuring DTMIs namespace conformance for model"));
            }

            // Import the same model to ensure its skipped.
            (returnCode, standardOut, _) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo} {strictSwitch}");

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            modelFile = new FileInfo(Path.GetFullPath(testImportRepo.FullName + "/" + modelFilePath));
            Assert.AreEqual(lastWriteTimeUtc, modelFile.LastWriteTimeUtc);
            Assert.True(standardOut.Contains($"Skipping \"{expectedDtmi}\". Model file already exists in repository."));
        }

        [TestCase(
            "dtmi/com/example/temperaturecontroller-1.expanded.json",
            "dtmi:com:example:TemperatureController;1,dtmi:com:example:Thermostat;1,dtmi:azure:DeviceManagement:DeviceInformation;1",
            "dtmi/com/example/temperaturecontroller-1.json,dtmi/com/example/thermostat-1.json,dtmi/azure/devicemanagement/deviceinformation-1.json",
            true)]
        public void ImportModelFileExpandedModelArray(string modelFilePath, string expectedDeps, string expectedPaths, bool strict)
        {
            // TODO: Revisit.
            ResetTestRepoDir();

            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string strictSwitch = strict ? "--strict" : "";

            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo} {strictSwitch}");

            string[] dtmis = expectedDeps.Split(",", StringSplitOptions.RemoveEmptyEntries);
            string[] paths = expectedPaths.Split(",", StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.False(standardError.Contains("Error:"));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));

            for (int i = 0; i < dtmis.Length; i++)
            {
                Assert.True(standardOut.Contains($"- Importing model \"{dtmis[i]}\"..."));
                FileInfo modelFile = new FileInfo(Path.GetFullPath(testImportRepo.FullName + "/" + paths[i]));
                Assert.True(modelFile.Exists);
                Assert.AreEqual(dtmis[i], ParsingUtils.GetRootId(modelFile));
            }
        }

        [TestCase("dtmi/com/example/invalidmodel-2.json")]
        public void ImportModelFileErrorInvalidDTDL(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.ValidationError, returnCode);

            Assert.True(standardError.Contains("Error:"));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
        }

        [TestCase("dtmi/com/example/invalidmodel-1.json")]
        public void ImportModelFileErrorResolutionFailure(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.ResolutionError, returnCode);

            Assert.True(standardError.Contains("Error:"));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
        }

        [TestCase("dtmi/strict/namespaceconflict-1.json", "dtmi:strict:namespaceconflict;1", "dtmi:com:example:acceleration;1")]
        public void ImportModelFileErrorStrictRuleIdNamespaceConformance(string modelFilePath, string rootDtmi, string violationDtmi)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo} --strict");

            Assert.AreEqual(Handlers.ReturnCodes.ValidationError, returnCode);

            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
            Assert.True(standardOut.Contains($"- Ensuring DTMIs namespace conformance for model \"{rootDtmi}\"..."));
            Assert.True(standardError.Contains($"Error: "));
            Assert.True(standardError.Contains(violationDtmi));
        }

        [TestCase("dtmi/strict/nondtdl-1.json")]
        public void ImportModelFileErrorNonDtdlContent(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, _, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.InvalidArguments, returnCode);
            Assert.True(standardError.Contains("Error: Importing model file contents of kind String is not yet supported."));
        }

        [TestCase("dtmi/strict/emptyarray-1.json")]
        public void ImportModelFileErrorEmptyJsonArray(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, _, string standardError) =
                ClientInvokator.Invoke($"import --model-file \"{qualifiedModelFilePath}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.ValidationError, returnCode);
            Assert.True(standardError.Contains("Error: No models to import."));
        }

        [TestCase("dtmi/com/example/thermostat-1.json")]
        public void ImportModelFileSilentNoStandardOut(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --silent --model-file \"{qualifiedModelFilePath}\" {targetRepo}");

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.True(!standardError.Contains("Error:"));
            Assert.AreEqual(string.Empty, standardOut);
        }

        [TestCase("dtmi/com/example/thermostat-1.json")]
        public void ImportSupportsDebugHeaders(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string targetRepo = $"--local-repo \"{testImportRepo.FullName}\"";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"import --silent --model-file \"{qualifiedModelFilePath}\" {targetRepo} --debug");

            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
            Assert.True(!standardError.Contains("Error:"));
            Assert.AreEqual(string.Empty, standardOut);
            Assert.True(standardError.Contains(Outputs.DebugHeader));
        }
    }
}
