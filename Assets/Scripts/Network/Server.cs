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
        private TcpListener _tcpListener;
        private List<TcpClient> _connectedClients = new();
        private CancellationTokenSource _cancellationTokenSource;
        private UniTask _acceptClientsTask;
        private Dictionary<string, HashSet<TcpClient>> _roomParticipants = new();

        public Server()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            OnMessageReceived += ProcessClientMessage;
            EventBus.instance.OnSendChatMessage += OnSendChatMessage;
            EventBus.instance.OnCloseChat += OnLeftFromChat;
        }

        public async UniTask StartServer()
        {
            Debug.Log("Starting server...");

            _tcpListener = new TcpListener(IPAddress.Any, serverPort);
            _tcpListener.Start();
            _cancellationTokenSource = new CancellationTokenSource();
            
            _acceptClientsTask = AcceptClientsLoop(_cancellationTokenSource.Token);
            await _acceptClientsTask.SuppressCancellationThrow();
        }

        private async UniTask AcceptClientsLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                    _connectedClients.Add(client);
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
                StringBuilder messageBuilder = new StringBuilder();

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
                        string part = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(part);
                        
                        try
                        {
                            var completeMessage = messageBuilder.ToString();
                            IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                            OnMessageReceived?.Invoke(completeMessage, clientEndPoint);
                            messageBuilder.Clear();
                        }
                        catch (Exception jsonEx)
                        {
                            Debug.Log($"Partial or invalid JSON received: {messageBuilder}. Error: {jsonEx.Message}");
                        }
                    }
                    else
                    {
                        Debug.Log("No data received. Client likely disconnected.");
                        break;
                    }
                }
            }
            catch (SocketException ex)
            {
                Debug.LogError($"Error handling client: {ex.Message}");
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
            var clientEndPoints = GetClientList();
            var clientList = string.Join(", ", clientEndPoints);
            var serverMessage = new ServerMessage(MessageEnum.CLIENT_LIST, clientList);

            SendMessageToAllClients(serverMessage);
        }

        private List<string> GetClientList()
        {
            var clientEndPoints = _connectedClients
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
            var clientMessage = JsonUtility.FromJson<ServerMessage>(message);

            switch (clientMessage.MessageType)
            {
                case MessageEnum.CREATE_ROOM:
                    OnCreateRoom?.Invoke(clientMessage.MessageData);
                    var createClient = _connectedClients.FirstOrDefault(c => c.Client.RemoteEndPoint.Equals(clientEndPoint));
                    if (createClient != null)
                    {
                        AddClientToRoom(clientMessage.MessageData, createClient);
                        SendMessageToAllClients(clientMessage);
                    }
                    break;

                case MessageEnum.JOIN_ROOM:
                    OnJoinRoom?.Invoke(clientMessage.MessageData);
                    var joinClient = _connectedClients.FirstOrDefault(c => c.Client.RemoteEndPoint.Equals(clientEndPoint));
                    if (joinClient != null)
                    {
                        AddClientToRoom(clientMessage.MessageData, joinClient);
                    }
                    break;

                case MessageEnum.DELETE_ROOM:
                    OnDeleteRoom?.Invoke(clientMessage.MessageData);
                    //УВЕДОМИТЬ ВСЕХ
                    break;
                
                case MessageEnum.REMOVE_PARTICIPANT:
                    var deletedClient = _connectedClients.FirstOrDefault(c => c.Client.RemoteEndPoint.Equals(clientEndPoint));
                    if (deletedClient != null)
                    {
                        RemoveClientFromRoom(clientMessage.RoomId, deletedClient);
                    }

                    Debug.Log($"Принял инфу что клиент {deletedClient} покинул комнату {clientMessage.RoomId}");
                    Debug.Log($"Буду передавать всем участникам комнаты что этот вышел");
                    var chatServiceMessage = $"Participant with id {deletedClient.Client.RemoteEndPoint} is left";
                    SendMessageToRoom(clientMessage.RoomId, chatServiceMessage);
                    break;

                case MessageEnum.SEND_TO_ROOM:
                    if (!string.IsNullOrEmpty(clientMessage.RoomId) && !string.IsNullOrEmpty(clientMessage.MessageData))
                    {
                        EventBus.instance.OnSendChatMessage?.Invoke(clientMessage.RoomId, clientMessage.Nickname, clientMessage.MessageData);
                    }
                    
                    if (!string.IsNullOrEmpty(clientMessage.RoomId) && 
                        !string.IsNullOrEmpty(clientMessage.Nickname) &&
                        !string.IsNullOrEmpty(clientMessage.MessageData))
                    {
                        EventBus.instance.OnReceiveRoomMessage?.Invoke(
                            clientMessage.RoomId, 
                            clientMessage.Nickname, 
                            clientMessage.MessageData
                        );
                    }
                    break;
                
                case MessageEnum.DISCONNECT:
                    OnClientDisconnected?.Invoke(clientEndPoint);
                    break;

                default:
                    Debug.Log($"Unhandled message type from client: {clientMessage.MessageType}");
                    break;
            }
        }
        
        private void OnSendChatMessage(string roomName, string nickName, string message)
        {
            var serverMessage = new ServerMessage(MessageEnum.SEND_TO_ROOM, message, roomName, nickName);

            string jsonMessage = JsonUtility.ToJson(serverMessage);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);

            foreach (var client in _connectedClients)
            {
                if (client.Connected)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
            }
        }
        
        private void OnLeftFromChat(string roomId)
        {
            var serverMessage = new ServerMessage(MessageEnum.REMOVE_PARTICIPANT, messageData:string.Empty, roomId: roomId);
            string jsonMessage = JsonUtility.ToJson(serverMessage);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            foreach (var client in _connectedClients)
            {
                SendData(client, data); //уведомляем что клиент с функцией сервера покинул комнату
            }
        }
        
        public void SendMessageToAllClients(ServerMessage message)
        {
            string jsonMessage = JsonUtility.ToJson(message);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);

            foreach (var client in _connectedClients)
            {
                SendData(client, data);
            }
        }
        
        public void HandleRoomCommand(string roomId, MessageEnum messageType)
        {
            var message = new ServerMessage(messageType, roomId);
            SendMessageToAllClients(message);
        }
        
        public void AddClientToRoom(string roomId, TcpClient client)
        {
            if (!_roomParticipants.ContainsKey(roomId))
            {
                _roomParticipants[roomId] = new HashSet<TcpClient>();
            }

            if (!_roomParticipants[roomId].Contains(client))
            {
                _roomParticipants[roomId].Add(client);
                Debug.Log($"Client {client.Client.RemoteEndPoint} added to room {roomId}");   
            }
            else
            {
                Debug.Log($"Client {client.Client.RemoteEndPoint} already added to room {roomId}");
            }
        }
        
        public void RemoveClientFromRoom(string roomId, TcpClient client)
        {
            if (_roomParticipants.ContainsKey(roomId))
            {
                _roomParticipants[roomId].Remove(client);
                Debug.Log($"Client {client.Client.RemoteEndPoint} removed from room {roomId}");

                if (_roomParticipants[roomId].Count == 0)
                {
                    _roomParticipants.Remove(roomId);
                    Debug.Log($"Room {roomId} is empty and removed");
                }
            }
        }
        
        public void SendMessageToRoom(string roomId, string message)
        {
            if (!_roomParticipants.ContainsKey(roomId))
            {
                Debug.LogWarning($"Room {roomId} does not exist");
                return;
            }

            byte[] data = Encoding.UTF8.GetBytes(message);

            foreach (var client in _roomParticipants[roomId])
            {
                SendData(client, data);
            }
            Debug.Log($"Message sent to room {roomId}: {message}");
        }

        private void DisconnectClient(TcpClient client)
        {
            if (client != null)
            {
                IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                _connectedClients.Remove(client);
                client.Close();

                OnClientDisconnected?.Invoke(clientEndPoint);
                Debug.Log($"Client disconnected: {clientEndPoint}");
            }
        }

        void NotificateOthers()
        {
            var serverMessage = new ServerMessage(MessageEnum.SERVER_SHUTDOWN, string.Empty);
            SendMessageToAllClients(serverMessage);
        }

        void SendData(TcpClient client, byte[] data)
        {
            if (client.Connected)
            {
                client.GetStream().Write(data, 0, data.Length);
            }
        }
        
        private void Unsubscribe()
        {
            OnMessageReceived -= ProcessClientMessage;
            EventBus.instance.OnSendChatMessage -= OnSendChatMessage;
        }

        public void StopServer()
        {
            Debug.Log("Stopping server...");

            NotificateOthers();
            Unsubscribe();
            
            _cancellationTokenSource?.Cancel();

            if (_acceptClientsTask.Status == UniTaskStatus.Pending)
            {
                Debug.Log("Waiting for AcceptClientsLoop to finish...");
            }
            
            foreach (var client in _connectedClients)
            {
                client.Close();
            }

            _connectedClients.Clear();
            _tcpListener?.Stop();
        }
    }
}