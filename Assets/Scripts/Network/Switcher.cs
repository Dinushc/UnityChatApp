using System.Net;
using System.Threading;
using Network.Chat;
using UnityEngine;

namespace Network
{
    public class Switcher : MonoBehaviour
    {
        private Server server;
        private Client client;
        private Subscriber _subscriber;
        private bool isMasterClient;
        [SerializeField] private RoomCreationUI roomCreationUI; 
        private CancellationTokenSource _initCancellationTokenSrc;
        
        private async void Start()
        {
            _initCancellationTokenSrc = new CancellationTokenSource();
            client = new Client();
            _subscriber = new Subscriber();
            client.OnBecomeServer += OnBecomeServer;
            client.Subscribe();
            _subscriber.RoomActionSubscribe(roomCreationUI, client, server, OnRoomCreationRequest, OnRoomRemove, OnJoinToRoom);
            
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
                _subscriber.RoomActionUnsubscribe(client, server, OnRoomCreationRequest, OnRoomRemove, OnJoinToRoom);
                Debug.Log("No server found. Starting as server.");
                isMasterClient = true;
                client.OnBecomeServer -= OnBecomeServer;
                client.Unsubscribe();
                client = null;

                server = new Server();
                server.OnClientConnected += OnClientConnected;
                server.OnClientDisconnected += OnClientDisconnected;
                _subscriber.RoomActionSubscribe(roomCreationUI, client, server, OnRoomCreationRequest, OnRoomRemove, OnJoinToRoom);
                await server.StartServer();
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
            _subscriber.RoomActionUnsubscribe(client, server, OnRoomCreationRequest, OnRoomRemove, OnJoinToRoom);
            server?.StopServer();
            client?.Unsubscribe();
            client?.connector?.Dispose();
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