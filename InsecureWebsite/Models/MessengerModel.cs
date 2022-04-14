namespace InsecureWebsite.Models;

public class MessengerModel
{
    public MessageModel[] Messages { get; set; }

    public class MessageModel
    {
        public string FromUsername { get; set; }
        public string ToUsername { get; set; }
        public string Message { get; set; }
    }
}