namespace InsecureWebsite.Models;

public class ProfileModel
{
    public string Username { get; set; }

    public ProfileMessageModel[] Messages { get; set; }
}