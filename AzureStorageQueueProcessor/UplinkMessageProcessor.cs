//---------------------------------------------------------------------------------
// Copyright (c) September 2020, devMobile Software
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
namespace AzureStorageQueueProcessor
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Reflection;
   using System.Text.Json;
   using Microsoft.Azure.WebJobs;
   using Microsoft.Azure.WebJobs.Host;
   using Microsoft.Extensions.Logging;

   using log4net;
   using log4net.Config;
   using System.Collections.Concurrent;

   public static class UplinkMessageProcessor
   {
      static readonly ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
      static ConcurrentDictionary<string, PayloadV5> DevicesSeen = new ConcurrentDictionary<string, PayloadV5>();

      [FunctionName("UplinkMessageProcessor")]
      public static void Run([QueueTrigger("%UplinkQueueName%", Connection = "AzureStorageConnectionString")] string myQueueItem, ExecutionContext executionContext)
      {
         var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
         XmlConfigurator.Configure(logRepository, new FileInfo(Path.Combine(executionContext.FunctionAppDirectory, "log4net.config")));

         log.Info($"Uplink message function triggered: {myQueueItem}");

         PayloadV5 payloadMessage = (PayloadV5)JsonSerializer.Deserialize(myQueueItem, typeof(PayloadV5));
         PayloadV5 payload = (PayloadV5)DevicesSeen.GetOrAdd(payloadMessage.dev_id, payloadMessage);

         log.Info($"Uplink message DevEui:{payload.dev_id} First:{payload.metadata.time} Current:{payloadMessage.metadata.time} PayloadRaw:{payload.payload_raw}");
      }
   }

   // Fith version of classes for unpacking HTTP payload https://json2csharp.com/
   public class GatewayV5 // https://github.com/TheThingsNetwork/ttn/blob/36761935d1867ce2cd70a80ceef197a124e2d276/core/types/gateway_metadata.go
   {
      public string gtw_id { get; set; }
      public ulong timestamp { get; set; }
      public DateTime time { get; set; }
      public int channel { get; set; }
      public int rssi { get; set; }
      public double snr { get; set; }
      public int rf_chain { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public int altitude { get; set; }
   }

   public class MetadataV5
   {
      public string time { get; set; }
      public double frequency { get; set; }
      public string modulation { get; set; }
      public string data_rate { get; set; }
      public string coding_rate { get; set; }
      public List<GatewayV5> gateways { get; set; }
   }

   public class PayloadV5
   {
      public string app_id { get; set; }
      public string dev_id { get; set; }
      public string hardware_serial { get; set; }
      public int port { get; set; }
      public int counter { get; set; }
      public bool is_retry { get; set; }
      public string payload_raw { get; set; }
      // finally settled on an Object
      public Object payload_fields { get; set; }
      public MetadataV5 metadata { get; set; }
      public string downlink_url { get; set; }
   }
}
