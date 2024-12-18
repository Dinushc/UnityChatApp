namespace Network
{
    public static class TextUtility
    {
        public static string GetPureMessage(string message)
        {
            string[] parts = message.Split(':');

            if (parts.Length == 2)
            {
                return parts[1];
            } if (parts.Length == 3)
            {
                return parts[1] + ":" + parts[2];
            }
            if (parts.Length == 4)
            {
                return parts[1] + ":" + parts[2] + ":" + parts[3];
            }

            return "";
        }

        public static string GetRandomNickname()
        {
            var nick = "Player_" + UnityEngine.Random.Range(1000, 9999);
            return nick;
        }
    }
}