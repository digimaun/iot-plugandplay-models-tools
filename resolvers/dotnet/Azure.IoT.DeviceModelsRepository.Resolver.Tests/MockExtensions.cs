﻿using Moq;
using System;

namespace Azure.IoT.DeviceModelsRepository.Resolver.Tests
{
    public static class MockExtensions
    {
        /*
        public static void ValidateLog(this Mock<ILogger> mockLogger, string message, LogLevel level, Times times)
        {
            mockLogger.Verify(l =>
                l.Log(level,
                      It.IsAny<EventId>(),
                      It.Is<It.IsAnyType>((o, _) => o.ToString() == message),
                      It.IsAny<Exception>(),
                      It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), times);
        }
        */
    }
}
