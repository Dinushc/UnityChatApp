using System.Net;
using System.Threading;
using Network.Chat;
using UnityEngine;

namespace Network
{
    public class ConnectionHandler : MonoBehaviour
    {
        public Server server;
        public Client client;
        private bool isMasterClient;
        [SerializeField] private RoomCreationUI roomCreationUI; 
        private CancellationTokenSource _initCancellationTokenSrc;
        
        private async void Start()
        {
            _initCancellationTokenSrc = new CancellationTokenSource();
            client = new Client();
            client.OnBecomeServer += OnBecomeServer;
            client.Subscribe();
            RoomActionSubscribe();
            
            var serverFound = await client.TryFindServer();

            if (serverFound)
            {
                if (!_initCancellationTokenSrc.Token.IsCancellationRequested)
                {
                    Debug.Log("Server found! Acting as client.");
                    await client.ConnectToFoundServer();   
                }
            }
            else
            {
                RoomActionUnsubscribe();
                Debug.Log("No server found. Starting as server.");
                isMasterClient = true;
                client.OnBecomeServer -= OnBecomeServer;
                client.Unsubscribe();
                client = null;

                server = new Server();
                server.OnClientConnected += OnClientConnected;
                server.OnClientDisconnected += OnClientDisconnected;
                RoomActionSubscribe();
                await server.StartServer();
            }
        }

        void RoomActionSubscribe()
        {
            if (roomCreationUI != null)
            {
                roomCreationUI.OnCreateRoomRequested += OnRoomCreationRequest;
                roomCreationUI.OnDeleteRoom += OnRoomRemove;
                roomCreationUI.OnJoinToRoom += OnJoinToRoom;
            }

            if (client != null)
            {
                client.OnCreateRoom += OnCreateRoom;
                client.OnJoinRoom += OnJoinRoom;
                client.OnDeleteRoom += OnDeleteRoom;
            }
            
            if (server != null)
            {
                server.OnCreateRoom += OnCreateRoom;
                server.OnJoinRoom += OnJoinRoom;
                server.OnDeleteRoom += OnDeleteRoom;
            }
        }
        
        void RoomActionUnsubscribe()
        {
            if (roomCreationUI != null)
            {
                roomCreationUI.OnCreateRoomRequested -= OnRoomCreationRequest;
                roomCreationUI.OnDeleteRoom -= OnRoomRemove;
                roomCreationUI.OnJoinToRoom -= OnJoinToRoom;
            }
            
            if (client != null)
            {
                client.OnCreateRoom -= OnCreateRoom;
                client.OnJoinRoom -= OnJoinRoom;
                client.OnDeleteRoom -= OnDeleteRoom;
            }
            
            if (server != null)
            {
                server.OnCreateRoom -= OnCreateRoom;
                server.OnJoinRoom -= OnJoinRoom;
                server.OnDeleteRoom -= OnDeleteRoom;
            }
        }

        void OnRoomCreationRequest(string roomId)
        {
            if(isMasterClient)
                server.CreateRoom(roomId);
            else
                client.CreateRoom(roomId);
        }
        void OnRoomRemove(string roomId)
        {
            if(isMasterClient)
                server.RemoveRoom(roomId);
            else
                client.DeleteRoom(roomId);
        }
        void OnJoinToRoom(string roomId)
        {
            if(isMasterClient)
                server.JoinRoom(roomId);
            else
                client.JoinRoom(roomId);
        }

        private void OnClientConnected(IPEndPoint clientEndPoint)
        {
            Debug.Log($"New client connected: {clientEndPoint}");
            server?.BroadcastClientList();
        }

        private void OnClientDisconnected(IPEndPoint clientEndPoint)
        {
            server?.BroadcastClientList();
        }

        private void OnDestroy()
        {
            RoomActionUnsubscribe();
            server?.StopServer();
            client?.Unsubscribe();
            client?.connector?.Dispose();
        }

        void OnCreateRoom(string roomId)
        {
            if (roomCreationUI != null)
            {
                roomCreationUI.CreateRoomFromServer(roomId);   
            }
        }

        void OnJoinRoom(string roomId)
        {
            if (roomCreationUI != null)
            {
                roomCreationUI.JoinToRoomFromServer(roomId);  
            }
        }
        
        void OnDeleteRoom(string roomId)
        {
            if (roomCreationUI != null)
            {
                roomCreationUI.DeleteRoomFromServer(roomId);  
            }
        }

        async void OnBecomeServer()
        {
            isMasterClient = true;
            _initCancellationTokenSrc?.Cancel();
            client.OnBecomeServer -= OnBecomeServer;
            client.Unsubscribe();
            client = null;
            
            server = new Server();
            server.OnClientConnected += OnClientConnected;
            server.OnClientDisconnected += OnClientDisconnected;

            await server.StartServer();
        }
    }
}