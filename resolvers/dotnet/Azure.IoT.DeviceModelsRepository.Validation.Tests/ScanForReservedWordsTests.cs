﻿using Azure.IoT.DeviceModelsRepository.CLI;
using NUnit.Framework;

namespace Azure.IoT.DeviceModelsRepository.Validation.Tests
{
    public class ScanForReservedWordsTests
    {
        [Test]
        public void ValidateRootIdWithoutReservedWords()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:example:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Azure Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.True(Validations.ScanIdsForReservedWords(doc));

        }

        [Test]
        public void ValidateSubIdWithoutReservedWords()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:example:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Azure Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""@id"": ""dtmi:com:example:device:property;1"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.True(Validations.ScanIdsForReservedWords(doc));
        }

        [Test]
        public void ValidationFailsWhenRootIdContainsAzure()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:AzUrE:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Azure Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.False(Validations.ScanIdsForReservedWords(doc));
        }

        [Test]
        public void ValidationFailsWhenSubIdContainsAzure()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:test:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Azure Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""@id"": ""dtmi:com:test:device:Azure;1"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.False(Validations.ScanIdsForReservedWords(doc));
        }

        [Test]
        public void ValidationFailsWhenRootIdContainsMicrosoft()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:MicroSoft:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Microsoft Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.False(Validations.ScanIdsForReservedWords(doc));
        }

        [Test]
        public void ValidationFailsWhenSubIdContainsMicrosoft()
        {
             var doc = @"{
                ""@context"": ""dtmi:dtdl:context;2"",
                ""@id"": ""dtmi:com:test:device;1"",
                ""@type"": ""Interface"",
                ""displayName"": ""Microsoft Device"",
                ""contents"": [
                    {
                        ""@type"": ""Property"",
                        ""@id"": ""dtmi:com:test:device:microsoft;1"",
                        ""name"": ""Failure"",
                        ""schema"": ""boolean""
                    }
                ]
            }";
            //Assert.False(Validations.ScanIdsForReservedWords(doc));
        }

    }
}
