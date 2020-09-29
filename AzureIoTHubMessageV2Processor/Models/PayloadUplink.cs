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
namespace devMobile.TheThingsNetwork.MessageProcessor.Models
{
   using System;
   using System.Collections.Generic;

   using Newtonsoft.Json;

   // Production version of classes for unpacking HTTP payload https://json2csharp.com/
   public class Gateway // https://github.com/TheThingsNetwork/ttn/blob/36761935d1867ce2cd70a80ceef197a124e2d276/core/types/gateway_metadata.go
   {
      [JsonProperty("gtw_id")]
      public string GatewayId { get; set; }
      [JsonProperty("timestamp")]
      public ulong Timestamp { get; set; }
      [JsonProperty("time")]
      public DateTime ReceivedAtUtc { get; set; }
      [JsonProperty("channel")]
      public int Channel { get; set; }
      [JsonProperty("rssi")]
      public int Rssi { get; set; }
      [JsonProperty("snr")]
      public double Snr { get; set; }
      [JsonProperty("RFChain")]
      public int rf_chain { get; set; }
      [JsonProperty("latitude")]
      public double Latitude { get; set; }
      [JsonProperty("longitude")]
      public double Longitude { get; set; }
      [JsonProperty("altitude")]
      public int Altitude { get; set; }
   }

   public class Metadata
   {
      [JsonProperty("time")]
      public DateTime ReceivedAtUtc { get; set; }
      [JsonProperty("frequency")]
      public double Frequency { get; set; }
      [JsonProperty("modulation")]
      public string Modulation { get; set; }
      [JsonProperty("data_rate")]
      public string DataRate { get; set; }
      [JsonProperty("coding_rate")]
      public string CodingRate { get; set; }
      [JsonProperty("gateways")]
      public List<Gateway> Gateways { get; set; }
   }

   public class PayloadUplink
   {
      [JsonProperty("app_id")]
      public string ApplicationId { get; set; }
      [JsonProperty("dev_id")]
      public string DeviceId { get; set; }
      [JsonProperty("hardware_serial")]
      public string DeviceEui { get; set; }
      [JsonProperty("port")]
      public int Port { get; set; }
      [JsonProperty("counter")]
      public int Counter { get; set; }
      [JsonProperty("is_retry")]
      public bool IsRetry { get; set; }
      [JsonProperty("Payload_raw")]
      public string PayloadRaw { get; set; }
      // finally settled on an Object
      [JsonProperty("payload_fields")]
      public Object PayloadFields { get; set; }
      [JsonProperty("metadata")]
      public Metadata Metadata { get; set; }
      [JsonProperty("downlink_url")]
      public string DownlinkUrl { get; set; }
   }

}
