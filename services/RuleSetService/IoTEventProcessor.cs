﻿using Microsoft.Azure.EventHubs;
using Microsoft.Azure.EventHubs.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RuleSetService
{
    public class IoTEventProcessor : IEventProcessor
    {

        private static EventHubClient eventHubClient;
        private  string EventHubConnectionString;
        private  string MsgSvcEventHubName;

        private readonly ILogger _logger;

        public IoTEventProcessor(IConfiguration config, ILogger logger)
        {
            _logger = logger;

            EventHubConnectionString = config.GetValue<string>("IOT_E2E_EH_CONNECTIONSTRING");
            MsgSvcEventHubName = config.GetValue<string>("IOT_E2E_EH_MSG_SVC_NAME");
        }

        public Task CloseAsync(PartitionContext context, CloseReason reason)
        {
            _logger.LogInformation($"Processor Shutting Down. Partition '{context.PartitionId}', Reason: '{reason}'.");
            // close the eventhub client
            eventHubClient.Close();

            return Task.CompletedTask;
        }

        public Task OpenAsync(PartitionContext context)
        {
            var connectionStringBuilder = new EventHubsConnectionStringBuilder(EventHubConnectionString)
            {
                EntityPath = MsgSvcEventHubName
            };
            eventHubClient = EventHubClient.CreateFromConnectionString(connectionStringBuilder.ToString());

            _logger.LogInformation($"SimpleEventProcessor initialized. Partition: '{context.PartitionId}'");
            return Task.CompletedTask;
        }

        public Task ProcessErrorAsync(PartitionContext context, Exception error)
        {
            _logger.LogInformation($"Error on Partition: {context.PartitionId}, Error: {error.Message}");
            return Task.CompletedTask;
        }

        public async Task ProcessEventsAsync(PartitionContext context, IEnumerable<EventData> messages)
        {
            foreach (var eventData in messages)
            {
                var data = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                _logger.LogInformation($"Message received. Partition: '{context.PartitionId}', Data: '{data}'");

                var devid = eventData.SystemProperties["iothub-connection-device-id"].ToString();

                var newevent = new EventData(Encoding.UTF8.GetBytes(data));
                newevent.Properties.Add("iothub-connection-device-id", devid);

                try
                {
                    await eventHubClient.SendAsync(newevent, devid);
                }
                catch (Exception exception)
                {
                    _logger.LogError($"{DateTime.Now} > Exception: {exception.Message}");
                }
            }

            await context.CheckpointAsync();
        }
    }
}
