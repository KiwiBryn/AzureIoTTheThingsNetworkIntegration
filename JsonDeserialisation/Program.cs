//---------------------------------------------------------------------------------
// Copyright (c) August 2020, devMobile Software
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
namespace devMobile.TheThingsNetwork.JsonDeserialisation
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Text.Json;

   public class Gateway // https://github.com/TheThingsNetwork/ttn/blob/36761935d1867ce2cd70a80ceef197a124e2d276/core/types/gateway_metadata.go
   {
      public string gtw_id { get; set; }
      public long timestamp { get; set; }
      public DateTime time { get; set; }
      public int channel { get; set; }
      public int rssi { get; set; }
      public double snr { get; set; }
      public int rf_chain { get; set; }
      public double latitude { get; set; }
      public double longitude { get; set; }
      public int altitude { get; set; }
   }

   public class Metadata
   {
      public string time { get; set; }
      public double frequency { get; set; }
      public string modulation { get; set; }
      public string data_rate { get; set; }
      public string coding_rate { get; set; }
      public List<Gateway> gateways { get; set; }
   }

   public class Payload
   {
      public string app_id { get; set; }
      public string dev_id { get; set; }
      public string hardware_serial { get; set; }
      public int port { get; set; }
      public int counter { get; set; }
      public bool is_retry { get; set; }
      public string payload_raw { get; set; }
      public Object payload_fields { get; set; }
      public Metadata metadata { get; set; }
      public string downlink_url { get; set; }
   }

   class Program
   {
      static void EnumerateChildren( int indent, JsonElement jsonElement )
      {
         foreach (var property in jsonElement.EnumerateObject())
         {
            JsonElement child = (JsonElement)property.Value;

            string prepend = string.Empty;
            for( int index =0; index < indent; index++)
            {
               prepend += " ";
            }
               
            if (child.ValueKind == JsonValueKind.Object)
            {
               Console.WriteLine($"{prepend}Name:{property.Name}");
               EnumerateChildren(indent + 3, child);
            }
            else
            {
               Console.WriteLine($"{prepend}Name:{property.Name} Value:{property.Value}");
            }
         }
      }


      static void Main(string[] args)
      {
         try
         {
            using (StreamReader r = new StreamReader(args[0]))
            {
               string json = r.ReadToEnd();
               Payload payload = JsonSerializer.Deserialize<Payload>(json);

               JsonElement jsonElement = (JsonElement)payload.payload_fields;

               EnumerateChildren(0, jsonElement);
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
         }

         Console.WriteLine();
         Console.WriteLine("Press <enter> to exit");
         
         Console.ReadLine();
      }
   }
}
