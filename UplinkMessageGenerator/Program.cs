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
namespace devMobile.TheThingsNetwork.UplinkMessageGenerator
{
   using System;
   using System.IO;

   using Azure.Storage.Queues;

   class Program
   {
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

            for (int i = 0; i < 500; i++)
            {
               if (( i % 100 ) == 0)
               {
                  Console.WriteLine();
               }
               queueClient.SendMessage(Convert.ToBase64String(File.ReadAllBytes(filename)));
               Console.Write(".");
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
