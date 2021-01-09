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
// https://github.com/ppatierno/codesamples/blob/master/IoTHubAmqp/IoTHubAmqp/Program.cs
//---------------------------------------------------------------------------------
#if RECEIVE_THREAD && RECEIVE_EVENT
#error Only one of RECEIVE_THREAD && RECEIVE_EVENT can be defined
#endif
#if !RECEIVE_THREAD && !RECEIVE_EVENT
#error One of RECEIVE_THREAD && RECEIVE_EVENT must be defined
#endif

namespace devMobile.TheThingsNetwork.AzureIoTHubAMQPNetLite
{
   using System;
   using System.Security.Cryptography;
   using System.Text;
#if RECEIVE_THREAD
   using System.Threading;
#endif
   using System.Threading.Tasks;
   using System.Web;

   using Amqp;
   using Amqp.Framing;

   class Program
   {
      private const int AmqpsPort = 5671;

      private static string server;
      private static string accessKey;
      private static string deviceId;
      private static string payload;

      private static Address address;
      private static Connection connection;
      private static Session session;

#if RECEIVE_THREAD
      private static Thread receiverThread;
#endif

      static async Task Main(string[] args)
      {
         Amqp.Trace.TraceLevel = Amqp.TraceLevel.Frame | Amqp.TraceLevel.Verbose;
         Amqp.Trace.TraceListener = (l, f, a) => System.Diagnostics.Trace.WriteLine(DateTime.Now.ToString("[hh:ss.fff]") + " " + Fx.Format(f, a));

         if (args.Length != 4)
         {
            Console.WriteLine("[MQTT Server] [DeviceId] [AccessKey] [payload]");
            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
            return;
         }

         server = args[0];
         deviceId = args[1];
         accessKey = args[2];
         payload = args[3];

         Console.WriteLine($"MQTT Server:{server} DeviceID:{deviceId} Payload:{payload}");
         Console.WriteLine();

         address = new Address(server, AmqpsPort, null, null);
         connection = new Connection(address);

         string audience = Fx.Format("{0}/devices/{1}", server, deviceId);
         string resourceUri = Fx.Format("{0}/devices/{1}", server, deviceId);

         string sasToken = GenerateSasToken(resourceUri, accessKey, null, new TimeSpan(1, 0, 0));
         bool cbs = PutCbsToken(connection, sasToken, audience);

         if (cbs)
         {
            session = new Session(connection);

#if RECEIVE_THREAD
            receiverThread = new Thread(ReceiveCommandsLoop);
            receiverThread.Start();

            receiverThread.Join();
#endif
            string entity = Fx.Format("/devices/{0}/messages/deviceBound", deviceId);

#if RECEIVE_EVENT
            ReceiverLink receiveLink = new ReceiverLink(session, "receive-link", entity);

            receiveLink.Start(200, OnMessage);
#endif
            DateTime LastSentAt = DateTime.UtcNow;
            Console.WriteLine("Press any key to exit");
            while (!Console.KeyAvailable)
            {
               await Task.Delay(100);
               if ((DateTime.UtcNow - LastSentAt) > new TimeSpan(0, 0, 30))
               {
                  Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} SendEvent Start");
                  await SendEvent();
                  Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss} SendEvent Finish");

                  LastSentAt = DateTime.UtcNow;
               }
            }

#if RECEIVE_EVENT
            await receiveLink.CloseAsync();
#endif
            await session.CloseAsync();
            await connection.CloseAsync();
         }
         Console.WriteLine("Press <ENTER> to exit");
         Console.ReadLine();
      }

      static private async Task SendEvent()
      {
         string entity = Fx.Format("/devices/{0}/messages/events", deviceId);

         SenderLink senderLink = new SenderLink(session, "sender-link", entity);

         var messageValue = Encoding.UTF8.GetBytes(payload);
         Message message = new Message()
         {
            BodySection = new Data() { Binary = messageValue }
         };

         await senderLink.SendAsync(message);
         await senderLink.CloseAsync();
      }

#if RECEIVE_THREAD
      static private async void ReceiveCommandsLoop()
      {
         string entity = Fx.Format("/devices/{0}/messages/deviceBound", deviceId);

         ReceiverLink receiveLink = new ReceiverLink(session, "receive-link", entity);

         while (true)
         {
            Message received = await receiveLink.ReceiveAsync();
            if (received != null)
            {
               Data data = (Data)received.BodySection;

               Console.WriteLine(UTF8Encoding.UTF8.GetString(data.Binary));
               if (received.ApplicationProperties != null)
               {
                  foreach (var property in received.ApplicationProperties.Map)
                  {
                     Console.WriteLine($" Key:{property.Key} Value:{property.Value}");
                  }
               }
               receiveLink.Accept(received);
            }
         }
         //await receiveLink.CloseAsync();
      }
#endif

#if RECEIVE_EVENT
      static private void OnMessage(IReceiverLink receiveLink, Message message)
      {
         Data data = (Data)message.BodySection;

         Console.WriteLine(UTF8Encoding.UTF8.GetString(data.Binary));
         if (message.ApplicationProperties != null)
         {
            foreach (var property in message.ApplicationProperties.Map)
            {
               Console.WriteLine($" Key:{property.Key} Value:{property.Value}");
            }
         }

         receiveLink.Accept(message);
      }
#endif

      static private bool PutCbsToken(Connection connection, string shareAccessSignature, string audience)
      {
         bool result = true;
         Session session = new Session(connection);

         string cbsReplyToAddress = "cbs-reply-to";
         var cbsSender = new SenderLink(session, "cbs-sender", "$cbs");
         var cbsReceiver = new ReceiverLink(session, cbsReplyToAddress, "$cbs");

         // construct the put-token message
         var request = new Message(shareAccessSignature)
         {
            Properties = new Properties
            {
               MessageId = Guid.NewGuid().ToString(),
               ReplyTo = cbsReplyToAddress
            },
            ApplicationProperties = new ApplicationProperties()
         };
         request.ApplicationProperties["operation"] = "put-token";
         request.ApplicationProperties["type"] = "azure-devices.net:sastoken";
         request.ApplicationProperties["name"] = audience;
         cbsSender.Send(request);

         // receive the response
         var response = cbsReceiver.Receive();
         if (response == null || response.Properties == null || response.ApplicationProperties == null)
         {
            result = false;
         }
         else
         {
            int statusCode = (int)response.ApplicationProperties["status-code"];
            // string statusCodeDescription = (string)response.ApplicationProperties["status-description"];
            if (statusCode != (int)202 && statusCode != (int)200) // !Accepted && !OK
            {
               result = false;
            }
         }

         // the sender/receiver may be kept open for refreshing tokens
         cbsSender.Close();
         cbsReceiver.Close();
         session.Close();

         return result;
      }

      public static string GenerateSasToken(string resourceUri, string key, string policyName, TimeSpan timeToLive)
      {
         DateTimeOffset expiryDateTimeOffset = new DateTimeOffset(DateTime.UtcNow.Add(timeToLive));

         string expiryEpoch = expiryDateTimeOffset.ToUnixTimeSeconds().ToString();
         string stringToSign = HttpUtility.UrlEncode(resourceUri) + "\n" + expiryEpoch;
         string signature;

         using (var hmac = new HMACSHA256(Convert.FromBase64String(key)))
         {
            signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
         }

         string token = $"SharedAccessSignature sr={HttpUtility.UrlEncode(resourceUri)}&sig={HttpUtility.UrlEncode(signature)}&se={expiryEpoch}";

         if (!String.IsNullOrEmpty(policyName))
         {
            token += "&skn=" + policyName;
         }

         return token;
      }
   }
}