//---------------------------------------------------------------------------------
// Copyright (c) November 2020, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
namespace devMobile.TheThingsNetwork.TTNMQTTIntegrationClient
{
   using System;
   using System.Diagnostics;
   using System.Threading.Tasks;

   using MQTTnet;
   using MQTTnet.Client;
   using MQTTnet.Client.Disconnecting;
   using MQTTnet.Client.Options;
   using MQTTnet.Client.Receiving;

   class Program
   {
      private static IMqttClient mqttClient = null;
      private static IMqttClientOptions mqttOptions = null;
      private static string server;
      private static string applicationId;
      private static string accessKey;
      private static string clientId;
      private static string deviceId;
      private static string payload;

      static async Task Main(string[] args)
      {
         MqttFactory factory = new MqttFactory();
         mqttClient = factory.CreateMqttClient();

         if (args.Length != 6)
         {
            Console.WriteLine("[MQTT Server] [ApplicationId] [AccessKey] [ClientID] [DeviceId] [payload]");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }

         server = args[0];
         applicationId = args[1];
         accessKey = args[2];
         clientId = args[3];
         deviceId = args[4];
         payload = args[5];

         Console.WriteLine($"MQTT Server:{server} ApplicationID:{applicationId} ClientID:{clientId} deviceID:{deviceId} Feedname:{payload}");

         mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(server)
            .WithCredentials(applicationId, accessKey)
            .WithClientId(clientId)
            .WithTls()
            .Build();


         mqttClient.UseDisconnectedHandler(new MqttClientDisconnectedHandlerDelegate(e => MqttClient_Disconnected(e)));
         mqttClient.UseApplicationMessageReceivedHandler(new MqttApplicationMessageReceivedHandlerDelegate(e => MqttClient_ApplicationMessageReceived(e)));

         await mqttClient.ConnectAsync(mqttOptions);

         string uplinktopic = $"{applicationId}/devices/+/up";
         //string uplinktopic = $"{applicationId}/devices/{deviceId}/up";
         await mqttClient.SubscribeAsync(uplinktopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

         string downlinkAcktopic = $"{applicationId}/devices/{deviceId}/events/down/acks";
         await mqttClient.SubscribeAsync(downlinkAcktopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

         string downlinkScheduledtopic = $"{applicationId}/devices/{deviceId}/events/down/scheduled";
         await mqttClient.SubscribeAsync(downlinkScheduledtopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

         string downlinkSenttopic = $"{applicationId}/devices/{deviceId}/events/down/sent";
         await mqttClient.SubscribeAsync(downlinkSenttopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

         string downlinktopic = $"{applicationId}/devices/{deviceId}/down";

         DateTime LastSentAt = DateTime.UtcNow;
         Console.WriteLine("Press any key to exit");
         while (!Console.KeyAvailable)
         {
            await Task.Delay(100);
            if ((DateTime.UtcNow - LastSentAt) > new TimeSpan(0, 0, 30))
            {
               var message = new MqttApplicationMessageBuilder()
                  .WithTopic(downlinktopic)
                  .WithPayload(payload)
                  .WithAtLeastOnceQoS()
               .Build();

               LastSentAt = DateTime.UtcNow;

               Console.WriteLine($"{DateTime.UtcNow:HH:MM:ss} PublishAsync Start");
               await mqttClient.PublishAsync(message);
               Console.WriteLine($"{DateTime.UtcNow:HH:MM:ss} PublishAsync Finish");
            }
         }
      }

      private static void MqttClient_ApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
      {
         if (e.ApplicationMessage.Topic.EndsWith("/up"))
         {
            Console.WriteLine($"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic} Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
         }
         else
         {
            Console.WriteLine($"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic}");
         }
      }

      private static async void MqttClient_Disconnected(MqttClientDisconnectedEventArgs e)
      {
         Debug.WriteLine($"Disconnected:{e.ReasonCode}");
         await Task.Delay(TimeSpan.FromSeconds(5));

         try
         {
            await mqttClient.ConnectAsync(mqttOptions);
         }
         catch (Exception ex)
         {
            Debug.WriteLine("Reconnect failed {0}", ex.Message);
         }
      }
   }
}
