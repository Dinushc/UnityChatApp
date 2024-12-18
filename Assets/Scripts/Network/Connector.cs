using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class Connector
    {
        private TcpClient tcpClient;
        private CancellationTokenSource connectCancellationTokenSource;
        private CancellationTokenSource listeningCancellationTokenSource;

        public Action OnConnected;
        public Action OnDisconnected;
        public Action<string> OnMessageReceived;

        public Connector(bool isServer, int serverPort = 3333, int clientPort = 0)
        {
            tcpClient = new TcpClient();
            connectCancellationTokenSource = new CancellationTokenSource();
            listeningCancellationTokenSource = new CancellationTokenSource();
        }

        public async UniTask<bool> TryConnect(int port)
        {
            if (!connectCancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await tcpClient.ConnectAsync(IPAddress.Loopback, port);
                    connectCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    OnConnected?.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log($"No connection");
                    OnDisconnected?.Invoke();
                    return false;
                }
            }

            return false;
        }

        public async UniTask StartListening()
        {
            try
            {
                NetworkStream stream = tcpClient.GetStream();
                byte[] buffer = new byte[1024];

                while (!listeningCancellationTokenSource.Token.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, listeningCancellationTokenSource.Token);
                    listeningCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Listening error: {ex.Message}");
                OnDisconnected?.Invoke();
            }
        }

        public void SendMessage(string message)
        {
            if (tcpClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                tcpClient.GetStream().Write(data, 0, data.Length);
            }
        }

        public bool WillBeNewServer(IPEndPoint ipAddress)
        {
            return Equals(tcpClient.Client.LocalEndPoint, ipAddress);
        }

        public void Disconnect()
        {
            OnDisconnected?.Invoke();
            tcpClient?.Close();
            tcpClient?.Dispose();
        }

        public void Dispose()
        {
            Disconnect();
            connectCancellationTokenSource?.Cancel();
            listeningCancellationTokenSource?.Cancel();
        }
    }
}