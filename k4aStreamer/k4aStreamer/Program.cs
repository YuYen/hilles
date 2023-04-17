// Copyright 2015 gRPC authors.
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

using System;
using System.Threading.Tasks;
using System.Timers;
using Grpc.Core;
using Grpc.Core.Logging;
using k4aStreamer.network;
using k4aStreamer.proto.Impl;

namespace k4aStreamer
{
    class Program
    {
        public static readonly ConsoleLogger logger = new ConsoleLogger();
        public static readonly Timer regularReporter = new Timer(5000);
        
        const int Port = 30051;

        public static void Main(string[] args)
        {
            //// uncomment the following line to turn on wifi hotspot at beginning 
            // MobileHotspot.StartHotSpot().Wait();
            
            DiscoveryService.Start();

            regularReporter.Enabled = true;
            
            Server server = new Server
            {
                Services = { K4aService.BindService(new K4AServiceImpl2()) },
                Ports = { new ServerPort("0.0.0.0", Port, ServerCredentials.Insecure) }
            };
            server.Start();
            
            Console.WriteLine("Greeter server listening on port " + Port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}