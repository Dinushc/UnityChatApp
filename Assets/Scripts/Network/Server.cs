using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using UnityEngine;

namespace Network
{
    public class Server
    {
        public event Action<string, IPEndPoint> OnMessageReceived;
        public event Action<IPEndPoint> OnClientConnected;
        public event Action<IPEndPoint> OnClientDisconnected;
        private const int serverPort = 10000;
        public Action<string> OnCreateRoom;
        public Action<string> OnJoinRoom;
        public Action<string> OnDeleteRoom;
        private TcpListener tcpListener;
        private List<TcpClient> connectedClients = new();
        private Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        private CancellationTokenSource cancellationTokenSource;
        private UniTask acceptClientsTask;
        private Dictionary<string, HashSet<TcpClient>> roomParticipants = new();

        public Server()
        {
            OnMessageReceived += ProcessClientMessage;
            EventBus.instance.OnSendChatMessage += OnSendChatMessage;
        }

        public async UniTask StartServer()
        {
            Debug.Log("Starting server...");

            tcpListener = new TcpListener(IPAddress.Any, serverPort);
            tcpListener.Start();
            cancellationTokenSource = new CancellationTokenSource();
            
            acceptClientsTask = AcceptClientsLoop(cancellationTokenSource.Token);
            await acceptClientsTask.SuppressCancellationThrow();
        }

        private async UniTask AcceptClientsLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    connectedClients.Add(client);
                    IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;

                    OnClientConnected?.Invoke(clientEndPoint);

                    UniTask.Void(async () => await HandleClient(client, cancellationToken));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error accepting clients: {ex.Message}");
            }
        }

        private async UniTask HandleClient(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!IsClientConnected(client)) 
                    {
                        Debug.Log("Client disconnected detected.");
                        break;
                    }

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;

                        OnMessageReceived?.Invoke(message, clientEndPoint);
                    }
                    else
                    {
                        Debug.Log("No data received. Client likely disconnected.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling client: {ex.Message}");
            }
            finally
            {
                DisconnectClient(client);
            }
        }
        
        public void BroadcastClientList()
        {
            List<string> clientEndPoints = GetClientList();

            string clientListMessage = "*" + string.Join(",", clientEndPoints);
            SendMessageToAllClients(clientListMessage);
        }

        private List<string> GetClientList()
        {
            var clientEndPoints = connectedClients
                .Where(c => c.Connected)
                .Select(c => (c.Client.RemoteEndPoint as IPEndPoint)?.ToString())
                .Where(ep => ep != null)
                .ToList();
            return clientEndPoints;
        }
        
        private bool IsClientConnected(TcpClient client)
        {
            try
            {
                return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public void ProcessClientMessage(string message, IPEndPoint clientEndPoint)
        {
            var pureMessage = TextUtility.GetPureMessage(message);
            if (message.StartsWith(MessageEnum.CREATE_ROOM.ToString()))
            {
                OnCreateRoom?.Invoke(pureMessage);
                var client = connectedClients.FirstOrDefault(c => c.Client.RemoteEndPoint.Equals(clientEndPoint));
                if (client != null)
                {
                    AddClientToRoom(pureMessage, client);
                    SendMessageToAllClients(message);
                }
            }
            else if (message.StartsWith(MessageEnum.JOIN_ROOM.ToString()))
            {
                OnJoinRoom?.Invoke(pureMessage);
                var client = connectedClients.FirstOrDefault(c => c.Client.RemoteEndPoint.Equals(clientEndPoint));
                if (client != null)
                {
                    AddClientToRoom(pureMessage, client);
                }
            }
            else if (message.StartsWith(MessageEnum.DELETE_ROOM.ToString()))
            {
                OnDeleteRoom?.Invoke(pureMessage);
            }
            else if (message.StartsWith(MessageEnum.SEND_TO_ROOM.ToString()))
            {
                var parts = pureMessage.Split(':', 2);
                if (parts.Length == 2)
                {
                    var roomId = parts[0];
                    var roomMessage = parts[1];
                    SendMessageToRoom(roomId, roomMessage);
                }
                var sparts = pureMessage.Split(':');
                var roomID = sparts[0];
                var nickname = sparts[1];
                var msg = sparts[2];
                EventBus.instance.OnReceiveRoomMessage?.Invoke(roomID, nickname, msg);
            }
            else
            {
                
            }
        }
        
        private void OnSendChatMessage(string roomName, string nickName, string message)
        {
            var msg = $"{MessageEnum.SEND_TO_ROOM.ToString()}:{roomName}:{nickName}:{message}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            foreach (var client in connectedClients)
            {
                if (client.Connected)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
            }
        }
        
        public void SendMessageToAllClients(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            foreach (var client in connectedClients)
            {
                if (client.Connected)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
            }
        }

        public void SendMessageToClient(TcpClient client, string message)
        {
            if (client.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                client.GetStream().Write(data, 0, data.Length);
            }
        }
        
        public void CreateRoom(string roomId)
        {
            var message = $"{MessageEnum.CREATE_ROOM.ToString()}:{roomId}";
            SendMessageToAllClients(message);
            Debug.Log($"Room created with ID: {roomId}");
        }

        public void RemoveRoom(string roomName)
        {
            var message = $"{MessageEnum.DELETE_ROOM.ToString()}:{roomName}";
            SendMessageToAllClients(message);
            Debug.Log($"Room deleted with ID: {roomName}");
        }
        
        public void JoinRoom(string roomName)
        {
            var message = $"{MessageEnum.JOIN_ROOM.ToString()}:{roomName}";
            SendMessageToAllClients(message);
            Debug.Log($"Join to room with ID: {roomName}");
        }
        
        public void AddClientToRoom(string roomId, TcpClient client)
        {
            if (!roomParticipants.ContainsKey(roomId))
            {
                roomParticipants[roomId] = new HashSet<TcpClient>();
            }

            roomParticipants[roomId].Add(client);
            Debug.Log($"Client {client.Client.RemoteEndPoint} added to room {roomId}");
        }

        public void RemoveClientFromRoom(string roomId, TcpClient client)
        {
            if (roomParticipants.ContainsKey(roomId))
            {
                roomParticipants[roomId].Remove(client);
                Debug.Log($"Client {client.Client.RemoteEndPoint} removed from room {roomId}");

                if (roomParticipants[roomId].Count == 0)
                {
                    roomParticipants.Remove(roomId); // Удаляем комнату, если она пуста
                    Debug.Log($"Room {roomId} is empty and removed");
                }
            }
        }

        public void RemoveClientFromAllRooms(IPEndPoint client)
        {
            foreach (var room in roomParticipants.Keys.ToList())
            {
                //RemoveClientFromRoom(room, client);
            }
        }
        
        public void SendMessageToRoom(string roomId, string message)
        {
            Debug.Log("Chat test: " + roomId + " | " + message);
            if (!roomParticipants.ContainsKey(roomId))
            {
                Debug.LogWarning($"Room {roomId} does not exist");
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var client in roomParticipants[roomId])
            {
                if (client.Connected)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
            }
            Debug.Log($"Message sent to room {roomId}: {message}");
        }


        private void DisconnectClient(TcpClient client)
        {
            if (client != null)
            {
                IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                connectedClients.Remove(client);
                client.Close();

                OnClientDisconnected?.Invoke(clientEndPoint);
                Debug.Log($"Client disconnected: {clientEndPoint}");
            }
        }

        void NotificateOthers()
        {
            SendMessageToAllClients(MessageEnum.SERVER_SHUTDOWN.ToString());
        }

        public void StopServer()
        {
            Debug.Log("Stopping server...");

            NotificateOthers();
            OnMessageReceived -= ProcessClientMessage;
            
            cancellationTokenSource?.Cancel();

            if (acceptClientsTask.Status == UniTaskStatus.Pending)
            {
                Debug.Log("Waiting for AcceptClientsLoop to finish...");
            }
            
            foreach (var client in connectedClients)
            {
                client.Close();
            }

            connectedClients.Clear();
            tcpListener?.Stop();
        }
    }
}