using Azure.Core.Diagnostics;
using System;
using System.Diagnostics.Tracing;

namespace Azure.IoT.DeviceModelsRepository.Resolver
{
    [EventSource(Name = EventSourceName)]
    internal sealed class AzureCoreEventSource : EventSource
    {
        // Set EventSource name to package name replacing . with -
        private const string EventSourceName = "Azure-IoT-DeviceModelsRepository-Resolver";

        // Having event ids defined as const makes it easy to keep track of them
        private const int MessageSentEventId = 1;
        private const int ClientClosingEventId = 2;

        public static AzureCoreEventSource Shared { get; } = new AzureCoreEventSource();

        private AzureCoreEventSource() : base(EventSourceName, EventSourceSettings.Default, AzureEventSourceListener.TraitName, AzureEventSourceListener.TraitValue) { }

        [NonEvent]
        public void MessageSent(string messageBody)
        {
            // We are calling Guid.ToString make sure anyone is listening before spending resources
            if (IsEnabled(EventLevel.Informational, EventKeywords.None))
            {
                MessageSent(this.Guid.ToString("N"), messageBody);
            }
        }

        // In this example we don't do any expensive parameter formatting so we can avoid extra method and IsEnabled check

        [Event(ClientClosingEventId, Level = EventLevel.Informational, Message = "Client {0} is closing the connection.")]
        public void ClientClosing(string clientId)
        {
            WriteEvent(ClientClosingEventId, clientId);
        }

        [Event(MessageSentEventId, Level = EventLevel.Informational, Message = "Client {0} sent message with body '{1}'")]
        private void MessageSent(string clientId, string messageBody)
        {
            WriteEvent(MessageSentEventId, clientId, messageBody);
        }
    }
}
