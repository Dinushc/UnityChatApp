using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DefaultNamespace;
using UnityEngine;

namespace Network
{
    public class Client
    {
        public Connector connector;
        private const int serverPort = 10000;
        public Action OnBecomeServer;
        public Action<string> OnCreateRoom;
        public Action<string> OnJoinRoom;
        public Action<string> OnDeleteRoom;
        private CancellationToken _findServerCancellationToken;
        private CancellationToken _becomeNewServer;
        private CancellationToken _reconnectToServer;
        private List<IPEndPoint> _connectedClients;

        public Client()
        {
            Init();
        }

        void Init()
        {
            connector = new Connector(false, clientPort: 0);
            _findServerCancellationToken = new CancellationToken();
            _becomeNewServer = new CancellationToken();
            _reconnectToServer = new CancellationToken();
            _connectedClients = new List<IPEndPoint>();
        }

        public void Subscribe()
        {
            connector.OnConnected += OnConnectedToServer;
            connector.OnMessageReceived += HandleServerMessage;
            connector.OnDisconnected += OnDisconnected;
            
            EventBus.instance.OnSendChatMessage += OnSendChatMessage;
            EventBus.instance.OnCloseChat += OnLeftFromChat;
        }

        private void OnSendChatMessage(string roomId, string nickName, string message)
        {
            var serverMessage = new ServerMessage(MessageEnum.SEND_TO_ROOM, message, roomId, nickName);
            connector.SendMessage(serverMessage);
        }

        private void OnLeftFromChat(string roomId)
        {
            Debug.Log("Отправил серверу инфу что я покинул комнату");
            var serverMessage = new ServerMessage(MessageEnum.REMOVE_PARTICIPANT, messageData:string.Empty, roomId: roomId);
            connector.SendMessage(serverMessage);
        }

        public void Unsubscribe()
        {
            connector.OnConnected -= OnConnectedToServer;
            connector.OnMessageReceived -= HandleServerMessage;
            connector.OnDisconnected -= OnDisconnected;
        }

        private async void HandleServerMessage(ServerMessage message)
        {
            switch (message.MessageType)
            {
                case MessageEnum.CREATE_ROOM:
                    OnCreateRoom?.Invoke(message.MessageData);
                    break;

                case MessageEnum.JOIN_ROOM:
                    OnJoinRoom?.Invoke(message.MessageData);
                    break;

                case MessageEnum.DELETE_ROOM:
                    OnDeleteRoom?.Invoke(message.MessageData);
                    break;

                case MessageEnum.SEND_TO_ROOM:
                    if (message.RoomId != null && message.Nickname != null)
                    {
                        EventBus.instance.OnReceiveRoomMessage?.Invoke(message.RoomId, message.Nickname, message.MessageData);
                    }
                    break;

                case MessageEnum.SERVER_SHUTDOWN:
                    await ChooseNewServer();
                    break;
                
                case MessageEnum.REMOVE_PARTICIPANT:
                    //OnLeftFromChat(message.RoomId, message.MessageData); //надо ли?
                    //сделать удаление только по запросу сервера, ждем от сервера номер комнаты и номер клиента которого удалить
                    break;

                case MessageEnum.CLIENT_LIST:
                    List<string> clients = new List<string>(message.MessageData.Split(','));
                    _connectedClients = ConvertToIPEndPoint(clients);
                    break;

                default:
                    Debug.Log($"Unhandled message from server: {message}");
                    break;
            }
        }

        private async UniTask ChooseNewServer()
        {
            if (_connectedClients.Count > 0)
            {
                var newServer = _connectedClients[0];
                if (connector.WillBeNewServer(newServer))
                {
                    Debug.Log("Will be new Server");
                    await BecomeNewServer();
                    _becomeNewServer.ThrowIfCancellationRequested();
                }
                else
                {
                    _reconnectToServer.ThrowIfCancellationRequested();
                    await WaitForNewServer();
                    _reconnectToServer.ThrowIfCancellationRequested();
                }
                _becomeNewServer.ThrowIfCancellationRequested();
            }
        }
        
        private List<IPEndPoint> ConvertToIPEndPoint(List<string> clientEndPoints)
        {
            List<IPEndPoint> endPoints = new List<IPEndPoint>();
            foreach (var client in clientEndPoints)
            {
                string[] parts = client.Split(':');
                string ipString = parts[0];
                ipString = ipString.Trim();
                int port = int.Parse(parts[1]);
                
                IPAddress ipAddress = IPAddress.Parse(ipString);
                IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
                endPoints.Add(endPoint);
            }
            return endPoints;
        }
        
        private async UniTask WaitForNewServer(bool mustSubscribe = true)
        {
            Debug.Log("Searching for a new server...");
            
            Init();
            Unsubscribe();
            await Task.Delay(50);
            if(mustSubscribe)
                Subscribe();

            bool serverFound = await TryFindServer();

            if (serverFound)
            {
                Debug.Log("New server found! Reconnecting...");
                await ConnectToFoundServer();
            }
            else
            {
                Debug.LogError("Failed to find a new server. Retrying...");
                await UniTask.Delay(2000);
                await WaitForNewServer(false);
            }
        }
        
        private async UniTask BecomeNewServer()
        {
            _becomeNewServer.ThrowIfCancellationRequested();
            connector.Dispose();
            connector = null;
            connector = new Connector(true);

            OnBecomeServer?.Invoke();
            _becomeNewServer.ThrowIfCancellationRequested();
        }

        public async UniTask<bool> TryFindServer(int retries = 3)
        {
            try
            {
                for (int i = 0; i < retries; i++)
                {
                    _findServerCancellationToken.ThrowIfCancellationRequested();
                    var response = await connector.TryConnect(serverPort);

                    if (response)
                    {
                        Debug.Log("Successfully connected to server.");
                        _findServerCancellationToken.ThrowIfCancellationRequested();
                        await connector.StartListening();
                        return true;
                    }
                    await Task.Delay(200);
                    Debug.LogWarning("The answer has not yet arrived");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Загрузка отменена");
            }
            catch (SocketException ex)
            {
                Debug.LogError($"Socket exception: {ex.Message}");
            }
            return false;
        }

        public async UniTask ConnectToFoundServer()
        {
            Debug.Log("Connecting to the found server...");
            await connector.TryConnect(serverPort);
            await connector.StartListening();
        }

        public void OnConnectedToServer()
        {
            Debug.Log("Connected to server!");
        }

        public void HandleRoomCommand(string roomId, MessageEnum messageType)
        {
            var serverMessage = new ServerMessage(messageType, roomId);
            connector.SendMessage(serverMessage);
        }

        public void OnDisconnected()
        {
            var serverMessage = new ServerMessage(MessageEnum.DISCONNECT, string.Empty);
            connector.SendMessage(serverMessage);
        }
    }
}