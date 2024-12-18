using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Network.Chat
{
    public class RoomsReceiver
    {
        public event Action<string, string, string> OnMessageReceived;
        private readonly Dictionary<string, HashSet<TcpClient>> roomParticipants = new();

        public void CreateRoom(string roomId)
        {
            if (roomParticipants.ContainsKey(roomId))
            {
                Debug.LogWarning($"Room {roomId} already exists.");
                return;
            }

            roomParticipants[roomId] = new HashSet<TcpClient>();
            Debug.Log($"Room created with ID: {roomId}");
        }

        public void RemoveRoom(string roomId)
        {
            if (!roomParticipants.Remove(roomId))
            {
                Debug.LogWarning($"Room {roomId} does not exist.");
            }
            else
            {
                Debug.Log($"Room {roomId} deleted.");
            }
        }

        public void AddClientToRoom(string roomId, TcpClient client)
        {
            if (!roomParticipants.ContainsKey(roomId))
            {
                Debug.LogWarning($"Room {roomId} does not exist. Creating it automatically.");
                CreateRoom(roomId);
            }

            roomParticipants[roomId].Add(client);
            Debug.Log($"Client {client.Client.RemoteEndPoint} added to room {roomId}");
        }

        public void RemoveClientFromRoom(string roomId, TcpClient client)
        {
            if (roomParticipants.TryGetValue(roomId, out var clients) && clients.Remove(client))
            {
                Debug.Log($"Client {client.Client.RemoteEndPoint} removed from room {roomId}");

                if (clients.Count == 0)
                {
                    roomParticipants.Remove(roomId);
                    Debug.Log($"Room {roomId} is empty and removed.");
                }
            }
        }

        public void SendMessageToRoom(string roomId, string message)
        {
            if (!roomParticipants.TryGetValue(roomId, out var clients))
            {
                Debug.LogWarning($"Room {roomId} does not exist.");
                return;
            }

            byte[] data = System.Text.Encoding.UTF8.GetBytes(message);
            foreach (var client in clients)
            {
                if (client.Connected)
                {
                    client.GetStream().Write(data, 0, data.Length);
                }
            }

            Debug.Log($"Message sent to room {roomId}: {message}");
        }

        public void HandleRoomMessage(string roomId, string nickname, string message)
        {
            OnMessageReceived?.Invoke(roomId, nickname, message);
        }
    }
}