using System.Net;
using System.Net.Sockets;

namespace Network
{
    public static class GetIpUtil
    {
        public static string GetLocalIPAddress()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address + endPoint?.Port.ToString();
            }
        }
    }
}