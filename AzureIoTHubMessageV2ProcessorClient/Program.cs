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
namespace devMobile.TheThingsNetwork.AzureIoTHubMessageV2ProcessorClient
{
   using System;
   using System.IO;
   using System.Text;
   using System.Threading;
   using Azure.Storage.Queues;

   class Program
   {
      const int DeviceMessagePayloadFileCount = 10;
      const int DevicesCount = 1000;

      static void Main(string[] args)
      {
         string filename;
         string queueName;
         string storageConnectionString;

         if (args.Length != 3)
         {
            Console.WriteLine("[JOSN file] [QueueName] [StorageConnectionString] ");
            Console.ReadLine();
            return;
         }
         filename = args[0];
         queueName = args[1];
         storageConnectionString = args[2];

         try
         {
            QueueClient queueClient = new QueueClient(storageConnectionString, queueName);

            queueClient.CreateIfNotExists();

            for (int messageCount = 0; messageCount < 24; messageCount++)
            {
               for (int deviceCounter = 0; deviceCounter < DevicesCount; deviceCounter++)
               {
                  int fileNameCounter = deviceCounter % DeviceMessagePayloadFileCount;
                  if ((deviceCounter % 100) == 0)
                  {
                     Console.WriteLine();
                  }

                  string fileName = string.Format(filename, fileNameCounter);

                  string payload = File.ReadAllText(fileName);

                  //For exercising the cache more to see what memory consumption is like
                  //payload = payload.Replace("@dev_id@", 1000 + deviceCounter.ToString("000"));
                  payload = payload.Replace("@dev_id@", deviceCounter.ToString("000"));
                  payload = payload.Replace("@time@", DateTime.UtcNow.ToString("s"));

                  queueClient.SendMessage(Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(payload)));
                  Console.Write(".");
                  Thread.Sleep(1000);
               }
               //Thread.Sleep(1000*60*5);
               Thread.Sleep(10000);
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
