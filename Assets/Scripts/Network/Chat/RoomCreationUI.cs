using System.Collections.Generic;
using System.Linq;
using Network.Chat.ChatRoomLogic;

namespace Network.Chat
{
    using UnityEngine;
    using UnityEngine.UI;
    using System;

    public class RoomCreationUI : MonoBehaviour
    {
        public Action<string> OnCreateRoomRequested;
        public Action<string> OnDeleteRoom;
        public Action<string> OnJoinToRoom;
        private string roomName;
        private ChatRoomPresenter chatRoomPresenter;
        [SerializeField] private ChatRoomView roomUIPrefab;
        [SerializeField] private Transform roomListContainer;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private ChatView _chatView;
        private List<string> roomNames;
        private List<string> myRooms;
        private RoomChatManager _roomChatManager;

        private void Start()
        {
            roomNames = new List<string>();
            myRooms = new List<string>();
            createRoomButton.onClick.AddListener(HandleCreateRoomClicked);
        }

        private string GenerateRoomName()
        {
            var guid = Guid.NewGuid();
            var shortGuid = Convert.ToBase64String(guid.ToByteArray());
            shortGuid = shortGuid.Replace("=", "").Replace("+", "").Replace("/", "");
            return GetIpUtil.GetLocalIPAddress() + shortGuid;
        }

        private void HandleCreateRoomClicked()
        {
            var generatedRoomName = GenerateRoomName();
            myRooms.Add(generatedRoomName);
            if (roomNames.Contains(generatedRoomName))
            {
                return;
            }
            
            _roomChatManager = new RoomChatManager(generatedRoomName);
            roomNames.Add(generatedRoomName);
            OnCreateRoomRequested?.Invoke(generatedRoomName);
            
            var roomUI = Instantiate(roomUIPrefab, roomListContainer);
            chatRoomPresenter = new ChatRoomPresenter(roomUI);
            SubscribeToPresenterEvents(chatRoomPresenter);
            chatRoomPresenter.SetRoomLabel(generatedRoomName);
        }

        void SubscribeToPresenterEvents(ChatRoomPresenter presenter)
        {
            presenter.OnEnterToRoom += JoinRoom;
            presenter.OnCloseRoom += RemoveRoomUI;
        }

        public void CreateRoomFromServer(string roomId)
        {
            if (roomNames.Contains(roomId))
            {
                return;
            }
            var roomUI = Instantiate(roomUIPrefab, roomListContainer);
            chatRoomPresenter = new ChatRoomPresenter(roomUI);
            SubscribeToPresenterEvents(chatRoomPresenter);
            chatRoomPresenter.SetRoomLabel(roomId);
        }
        
        public void JoinToRoomFromServer(string roomId)
        {
            _roomChatManager = new RoomChatManager(roomId);
            _chatView.OpenChat(_roomChatManager);
        }
        
        public void DeleteRoomFromServer(string roomId)
        {
            var rooms = roomListContainer.GetComponentsInChildren<ChatRoomView>();
            var room = rooms.FirstOrDefault(r => r.roomLabel.text.Equals(roomId));
            RemoveRoom(room);
        }
        
        public void RemoveRoomUI(ChatRoomView item)
        {
            var roomLabel = item.roomLabel.text;
            if (myRooms.Contains(roomLabel))
            {
                OnDeleteRoom?.Invoke(item.roomLabel.text);
                RemoveRoom(item);
                myRooms.Remove(roomLabel);
                return;
            }
            Debug.Log("You are not owner");
        }

        void RemoveRoom(ChatRoomView view)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        private void JoinRoom(string roomId)
        {
            if (_roomChatManager == null)
            {
                _roomChatManager = new RoomChatManager(roomId);
            }
            _chatView.OpenChat(_roomChatManager);
            OnJoinToRoom?.Invoke(roomId);
        }
    }

}