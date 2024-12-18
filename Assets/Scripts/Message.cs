using System;
using UnityEngine;

[Serializable]
public class Message
{
    public string Type;
    public string Content;
    public string SenderId;
    public string RoomId;

    public static string Serialize(Message message) => JsonUtility.ToJson(message);
    public static Message Deserialize(string json) => JsonUtility.FromJson<Message>(json);
}