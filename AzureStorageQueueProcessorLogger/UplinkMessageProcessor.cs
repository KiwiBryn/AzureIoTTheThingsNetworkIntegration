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
namespace devMobile.TheThingsNetwork.AzureStorageQueueProcessorLogger
{
   using System;
   using System.Collections.Concurrent;
   using System.Collections.Generic;
   using System.Text.Json;
   using System.Threading;

   using Microsoft.Azure.WebJobs;
   using Microsoft.Extensions.Logging;

   public static class UplinkMessageProcessor
   {
      const string RunTag = "Logger002";
      static readonly ConcurrentDictionary<string, PayloadV5> DevicesSeen = new ConcurrentDictionary<string, PayloadV5>();
      static int ConcurrentThreadCount = 0;
      static int MessagesProcessed = 0;

      [FunctionName("UplinkMessageProcessor")]
      public static void Run([QueueTrigger("ttnuplinkmessages", Connection = "AzureStorageConnectionString")] string myQueueItem, ILogger log)
      {
         try
         {
            PayloadV5 payloadMessage = (PayloadV5)JsonSerializer.Deserialize(myQueueItem, typeof(PayloadV5));
            PayloadV5 payload = (PayloadV5)DevicesSeen.GetOrAdd(payloadMessage.dev_id, payloadMessage);

            Interlocked.Increment(ref ConcurrentThreadCount);
            Interlocked.Increment(ref MessagesProcessed);

            log.LogInformation($"{MessagesProcessed} {RunTag} DevEui:{payload.dev_id} Threads:{ConcurrentThreadCount} First:{payload.metadata.time} Current:{payloadMessage.metadata.time} PayloadRaw:{payload.payload_raw}");

            Thread.Sleep(2000);

            Interlocked.Decrement(ref ConcurrentThreadCount);
         }
         catch (Exception ex)
         {
            log.LogError(ex,"Processing of Uplink message failed");

            throw;
         }
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
