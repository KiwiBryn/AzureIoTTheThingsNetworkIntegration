//---------------------------------------------------------------------------------
// Copyright (c) October 2020, devMobile Software
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
namespace devMobile.TheThingsNetwork.AzureIoTHubCommandAndMessageHandler
{
   using System;
   using System.IO;
   using System.Text;
   using System.Threading;
   using System.Threading.Tasks;

   using Microsoft.Azure.Devices.Client;
   using Newtonsoft.Json;

   class Program
   {
      private static string payload;

      static async Task Main(string[] args)
      {
         string filename;
         string azureIoTHubconnectionString;
         DeviceClient azureIoTHubClient;
         Timer MessageSender;
#if RECEIVE_THREAD
         Thread receiveThread;
#endif
         if (args.Length != 2)
         {
            Console.WriteLine("[JOSN file] [AzureIoTHubConnectionString]");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }

         filename = args[0];
         azureIoTHubconnectionString = args[1];

         try
         {
            payload = await File.ReadAllTextAsync(filename);

            // Open up the connection
            azureIoTHubClient = DeviceClient.CreateFromConnectionString(azureIoTHubconnectionString, TransportType.Amqp);

            await azureIoTHubClient.OpenAsync();

#if RECEIVE_THREAD
            receiveThread = new Thread(DoReceive);
            receiveThread.Start(azureIoTHubClient);
#endif

#if DIRECT_METHODS
            await azureIoTHubClient.SetMethodHandlerAsync("BoB", MethodCallback, null);
            await azureIoTHubClient.SetMethodDefaultHandlerAsync(MethodCallbackDefault, null);
#endif

#if TIMER_CALLBACK_ASYNC
            MessageSender = new Timer(TimerCallbackAsync, azureIoTHubClient, new TimeSpan(0, 0, 10), new TimeSpan(0, 0, 10));
#else
            MessageSender = new Timer(TimerCallbackSync, azureIoTHubClient, new TimeSpan(0, 0, 10), new TimeSpan(0, 0, 10));
#endif
            Console.WriteLine("Press any key to exit");
            while (!Console.KeyAvailable)
            {
               await Task.Delay(100);
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Main {ex.Message}");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
         }
      }

      public static void TimerCallbackSync(object state)
      {
         DeviceClient azureIoTHubClient = (DeviceClient)state;

         try
         {
            // I know having the payload as a global is a bit nasty but this is a demo..
            using (Message message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(payload))))
            {
               Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync start", DateTime.UtcNow);
               azureIoTHubClient.SendEventAsync(message).GetAwaiter();
               Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync finish", DateTime.UtcNow);
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"TimerCallbackSync {ex.Message}");
         }
      }

      static async void TimerCallbackAsync(object state)
      {
         DeviceClient azureIoTHubClient = (DeviceClient)state;

         try
         {
            // I know having the payload as a global is a bit nasty but this is a demo..
            using (Message message = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(payload))))
            {
               Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync start", DateTime.UtcNow);
               await azureIoTHubClient.SendEventAsync(message);
               Console.WriteLine(" {0:HH:mm:ss} AzureIoTHubDeviceClient SendEventAsync finish", DateTime.UtcNow);
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"TimerCallbackAsync {ex.Message}");
         }
      }

#if DIRECT_METHODS
      private static async Task<MethodResponse> MethodCallback(MethodRequest methodRequest, object userContext)
      {
         Console.WriteLine($"BoB handler method was called.");

         return new MethodResponse(200);
      }

      private static async Task<MethodResponse> MethodCallbackDefault(MethodRequest methodRequest, object userContext)
      {
         Console.WriteLine($"Default handler method {methodRequest.Name} was called.");

         return new MethodResponse(200);
      }
#endif

#if RECEIVE_THREAD
      private static async void DoReceive(object status)
      {
         try
         {
            while (true)
            {
               DeviceClient deviceClient = (DeviceClient)status;

               using (Message message = await deviceClient.ReceiveAsync())
               {
                  if (message != null)
                  {
                     Console.WriteLine();
                     Console.WriteLine($"MessageID:{message.MessageId}");
                     Console.WriteLine($"ComponentName:{message.ComponentName}");
                     Console.WriteLine($"ConnectionDeviceId:{message.ConnectionDeviceId}");
                     Console.WriteLine($"ConnectionModuleId:{message.ConnectionModuleId}");
                     Console.WriteLine($"ContentEncoding:{message.ContentEncoding}");
                     Console.WriteLine($"ContentType:{message.ContentType}");
                     Console.WriteLine($"CorrelationId:{message.CorrelationId}");
                     Console.WriteLine($"CreationTimeUtc:{message.CreationTimeUtc}");
                     Console.WriteLine($"DeliveryCount:{message.DeliveryCount}");
                     Console.WriteLine($"EnqueuedTimeUtc:{message.EnqueuedTimeUtc}");
                     Console.WriteLine($"ExpiryTimeUtc:{message.ExpiryTimeUtc}");
                     Console.WriteLine($"CreationTimeUtc:{message.InputName}");
                     Console.WriteLine($"IsSecurityMessage:{message.IsSecurityMessage}");
                     Console.WriteLine($"MessageSchema:{message.MessageSchema}");
                     Console.WriteLine($"SequenceNumber:{message.SequenceNumber}");
                     Console.WriteLine($"To:{message.To}");
                     Console.WriteLine($"UserId:{message.UserId}");
                     string messageBody = Encoding.UTF8.GetString(message.GetBytes());
                     Console.WriteLine($"Body:{messageBody}");

                     if (messageBody.EndsWith("R", StringComparison.InvariantCultureIgnoreCase))
                     {
                        Console.WriteLine($"RejectAsync");
                        await deviceClient.RejectAsync(message);
                     }
                     if (messageBody.EndsWith("C", StringComparison.InvariantCultureIgnoreCase))
                     {
                        Console.WriteLine($"CompleteAsync");
                        await deviceClient.CompleteAsync(message);
                     }
                     if (messageBody.EndsWith("A", StringComparison.InvariantCultureIgnoreCase))
                     {
                        Console.WriteLine($"AbandonAsync");
                        await deviceClient.AbandonAsync(message);
                     }

                     foreach (var property in message.Properties)
                     {
                        Console.WriteLine($"Key:{property.Key} Value:{property.Value}");
                     }

                     Console.WriteLine();
                  }
               }
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Receive loop {ex.Message}");
          }
      }
#endif
   }
}
