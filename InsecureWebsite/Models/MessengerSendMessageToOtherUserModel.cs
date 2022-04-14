using System.ComponentModel.DataAnnotations;

namespace InsecureWebsite.Models;

public class MessengerSendMessageToOtherUserModel
{
    [Required]
    [StringLength(20)]
    public string OtherUser { get; set; }
    [Required]
    [StringLength(2048)]
    public string Message { get; set; }
}