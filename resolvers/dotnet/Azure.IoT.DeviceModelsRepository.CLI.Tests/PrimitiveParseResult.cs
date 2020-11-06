using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Azure.IoT.DeviceModelsRepository.CLI.Tests
{
    internal class PrimitiveParseResult
    {
        public PrimitiveParseResult(string jsonText)
        {
            this.RawJsonText = jsonText;

        }
        public string RawJsonText { get; }
    }
}
