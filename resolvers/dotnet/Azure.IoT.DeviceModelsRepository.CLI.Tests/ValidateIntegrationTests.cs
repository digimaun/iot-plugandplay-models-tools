using NUnit.Framework;
using System.IO;

namespace Azure.IoT.DeviceModelsRepository.CLI.Tests
{
    public class ValidateIntegrationTests
    {
        [TestCase("dtmi/com/example/thermostat-1.json", false)]
        [TestCase("dtmi/com/example/thermostat-1.json", true)]
        public void ValidateModelFileSingleModelObject(string modelFilePath, bool strict)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string strictSwitch = strict ? "--strict" : "";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" " +
                $"{strictSwitch}");
            Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);

            Assert.False(standardError.Contains("ERROR:"));
            Assert.True(standardError.Contains(Outputs.StandardHeader));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));

            if (strict)
            {
                Assert.True(standardOut.Contains("- Ensuring DTMIs namespace conformance for model"));
                Assert.True(standardOut.Contains("- Ensuring model file path adheres to DMR path conventions..."));
            }
        }

        [TestCase("dtmi/com/example/temperaturecontroller-1.expanded.json", false)]
        [TestCase("dtmi/com/example/temperaturecontroller-1.expanded.json", true)]
        public void ValidateModelFileArrayOfModelObjects(string modelFilePath, bool strict)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);
            string strictSwitch = strict ? "--strict" : "";

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" " +
                $"{strictSwitch}");
            
            if (!strict)
            {
                Assert.AreEqual(Handlers.ReturnCodes.Success, returnCode);
                Assert.False(standardError.Contains("ERROR:"));
                Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
                return;
            }

            // TODO: --strict validation is not fleshed out for an array of models.
            Assert.True(standardError.Contains("ERROR:"));
        }

        [TestCase("dtmi/com/example/invalidmodel-2.json")]
        public void ValidateModelFileErrorInvalidDTDL(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" ");
            
            Assert.AreEqual(Handlers.ReturnCodes.ParserError, returnCode);

            Assert.True(standardError.Contains("ERROR:"));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
        }

        [TestCase("dtmi/com/example/invalidmodel-1.json")]
        public void ValidateModelFileErrorResolutionFailure(string modelFilePath)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" ");

            Assert.AreEqual(Handlers.ReturnCodes.ResolutionError, returnCode);

            Assert.True(standardError.Contains("ERROR:"));
            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
        }

        [TestCase("dtmi/strict/namespaceconflict-1.json", "dtmi:strict:namespaceconflict;1", "dtmi:com:example:acceleration;1")]
        public void ValidateModelFileErrorStrictRuleIdNamespaceConformance(string modelFilePath, string rootDtmi, string violationDtmi)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" --strict");

            Assert.AreEqual(Handlers.ReturnCodes.ValidationError, returnCode);

            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
            Assert.True(standardOut.Contains($"- Ensuring DTMIs namespace conformance for model \"{rootDtmi}\"..."));
            Assert.True(standardError.Contains($"ERROR: "));
            Assert.True(standardError.Contains(violationDtmi));
        }

        [TestCase("dtmi/strict/badfilepath-1.json", "dtmi:com:example:Freezer;1")]
        public void ValidateModelFileErrorStrictRuleIdFilePath(string modelFilePath, string rootDtmi)
        {
            string qualifiedModelFilePath = Path.Combine(TestHelpers.TestLocalModelRepository, modelFilePath);

            (int returnCode, string standardOut, string standardError) =
                ClientInvokator.Invoke($"" +
                $"validate --model-file \"{qualifiedModelFilePath}\" " +
                $"--repo \"{TestHelpers.TestLocalModelRepository}\" --strict");

            Assert.AreEqual(Handlers.ReturnCodes.ValidationError, returnCode);

            Assert.True(standardOut.Contains("- Validating models conform to DTDL..."));
            Assert.True(standardOut.Contains($"- Ensuring DTMIs namespace conformance for model \"{rootDtmi}\"..."));
            Assert.True(standardOut.Contains($"- Ensuring model file path adheres to DMR path conventions..."));
            Assert.True(standardError.Contains("ERROR: "));
            Assert.True(standardError.Contains($"File \"{Path.GetFullPath(qualifiedModelFilePath)}\" does not adhere to DMR path conventions. "));
        }
    }
}
