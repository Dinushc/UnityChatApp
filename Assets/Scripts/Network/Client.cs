using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private void OnSendChatMessage(string roomName, string nickName, string message)
        {
            var msg = $"{MessageEnum.SEND_TO_ROOM.ToString()}:{roomName}:{nickName}:{message}";
            connector.SendMessage(msg);
        }

        public void Unsubscribe()
        {
            connector.OnConnected -= OnConnectedToServer;
            connector.OnMessageReceived -= HandleServerMessage;
            connector.OnDisconnected -= OnDisconnected;
        }

        private async void HandleServerMessage(string message)
        {
            var pureMessage = TextUtility.GetPureMessage(message);
            if (message.StartsWith("*"))
            {
                message = message.TrimStart('*');
                List<string> clients = new List<string>(message.Split(','));
                _connectedClients = ConvertToIPEndPoint(clients);
            }
            else if (message == MessageEnum.SERVER_SHUTDOWN.ToString())
            {
                await ChooseNewServer();
            }
            else if(message.StartsWith(MessageEnum.CREATE_ROOM.ToString()))
            {
                OnCreateRoom?.Invoke(pureMessage);
            }
            else if(message.StartsWith(MessageEnum.JOIN_ROOM.ToString()))
            {
                OnJoinRoom?.Invoke(pureMessage);
            }
            else if(message.StartsWith(MessageEnum.DELETE_ROOM.ToString()))
            {
                OnDeleteRoom?.Invoke(pureMessage);
            }
            else if (message.StartsWith(MessageEnum.SEND_TO_ROOM.ToString()))
            {
                var parts = pureMessage.Split(':');
                var roomId = parts[0];
                var nickname = parts[1];
                var msg = parts[2];
                EventBus.instance.OnReceiveRoomMessage?.Invoke(roomId, nickname, msg);
            }
            else
            {
                Debug.Log($"Message from server: {message}");
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
            var endPoints = clientEndPoints
                .Select(s =>
                {
                    var parts = s.Split(':');
                    var ip = IPAddress.Parse(parts[0]);
                    var port = int.Parse(parts[1]);
                    return new IPEndPoint(ip, port);
                })
                .ToList();
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

        public void CreateRoom(string roomId)
        {
            var message = $"{MessageEnum.CREATE_ROOM.ToString()}:{roomId}";
            connector.SendMessage(message);
        }

        public void JoinRoom(string roomId)
        {
            var message = $"{MessageEnum.JOIN_ROOM.ToString()}:{roomId}";
            connector.SendMessage(message);
        }

        public void DeleteRoom(string roomId)
        {
            var message = $"{MessageEnum.DELETE_ROOM.ToString()}:{roomId}";
            connector.SendMessage(message);
        }
        
        public void SendChatMessage(string roomId, string message)
        {
            var msg = $"{MessageEnum.SEND_TO_ROOM.ToString()}:{roomId}:{message}";
            connector.SendMessage(msg);
        }

        public void OnDisconnected()
        {
            connector.SendMessage($"{MessageEnum.DISCONNECT.ToString()}");
        }
    }
}