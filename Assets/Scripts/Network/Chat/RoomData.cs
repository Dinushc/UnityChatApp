using System.Collections.Generic;

namespace Network
{
    public class RoomData
    {
        public List<PlayerData> connectedPlayers = new();
        public string roomName;
        public string ipAddress;
        public int port;
    }

    public class PlayerData
    {
        public string id;
        public string ipAddress;
        public int port;
        public bool isMasterClient = false;
    }
}