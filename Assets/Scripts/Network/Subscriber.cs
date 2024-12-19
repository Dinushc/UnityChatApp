using System;
using Network.Chat;
using UnityEngine;

namespace Network
{
    public class Subscriber
    {
        private RoomCreationUI _roomCreationUI;
        
        public void RoomActionSubscribe(RoomCreationUI roomCreationUI, Client client, Server server, Action<string> OnRoomCreationRequest, Action<string> OnRoomRemove, Action<string> OnJoinToRoom)
        {
            _roomCreationUI = roomCreationUI;
            if (_roomCreationUI != null)
            {
                _roomCreationUI.OnCreateRoomRequested += OnRoomCreationRequest;
                _roomCreationUI.OnDeleteRoom += OnRoomRemove;
                _roomCreationUI.OnJoinToRoom += OnJoinToRoom;
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
        
        public void RoomActionUnsubscribe(Client client, Server server, Action<string> OnRoomCreationRequest, Action<string> OnRoomRemove, Action<string> OnJoinToRoom)
        {
            if (_roomCreationUI != null)
            {
                _roomCreationUI.OnCreateRoomRequested -= OnRoomCreationRequest;
                _roomCreationUI.OnDeleteRoom -= OnRoomRemove;
                _roomCreationUI.OnJoinToRoom -= OnJoinToRoom;
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
        
        void OnCreateRoom(string roomId)
        {
            if (_roomCreationUI != null)
            {
                _roomCreationUI.CreateRoomFromServer(roomId);   
            }
        }

        void OnJoinRoom(string roomId)
        {
            if (_roomCreationUI != null)
            {
                _roomCreationUI.JoinToRoomFromServer(roomId);  
            }
        }
        
        void OnDeleteRoom(string roomId)
        {
            if (_roomCreationUI != null)
            {
                _roomCreationUI.DeleteRoomFromServer(roomId);  
            }
        }
    }
}