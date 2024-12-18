using System.Collections.Generic;
using System.Net;

namespace Network
{
    public class Room
    {
        public string RoomId { get; private set; }
        public string OwnerIPAddress { get; private set; }
        public List<string> MembersIPAddresses { get; private set; } = new List<string>();

        public Room(string roomId, string ownerIPAddress)
        {
            RoomId = roomId;
            OwnerIPAddress = ownerIPAddress;
            MembersIPAddresses.Add(ownerIPAddress);
        }

        public bool IsOwner(string ipAddress)
        {
            return OwnerIPAddress == ipAddress;
        }
    }

}