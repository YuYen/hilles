using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using k4aStreamer.utils;

namespace k4aStreamer.network
{
    public class DiscoveryService
    {
        private static Task _task;
        public static void Start()
        {
            if (_task != null && !_task.IsCompleted)
                return;

            _task = Task.Run(() =>IPDiscoveryService());
        }
        
        private static async Task IPDiscoveryService()
        {
            while (true)
            {
                UdpClient udpServer = new UdpClient(CommUtils.HILLES_SERV_CHECK_PORT);
                using (udpServer)
                {
                    try
                    {
                        IPEndPoint remoteEndPoint;

                        while (true)
                        {
                            var recData = udpServer.ReceiveAsync();
                            await recData;
                            remoteEndPoint = recData.Result.RemoteEndPoint;

                            Console.WriteLine("receive data from " + remoteEndPoint);
                            if (recData.Result.Buffer.SequenceEqual(CommUtils.HILLES_QUERY_TOKEN))
                            {
                                Console.WriteLine("confirm back to " + remoteEndPoint);
                                udpServer.Send(CommUtils.HILLES_CONF_TOKEN, CommUtils.HILLES_CONF_TOKEN.Length,
                                    remoteEndPoint);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("HILLES checking service restart");
                    }
                }
            }
        }
    }
}