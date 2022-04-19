namespace InsecureWebsite.Models;

public class ProfileMessageModel
{
    public int Id { get; set; }
    public bool DisplayRaw { get; set; }
    public bool Hide { get; set; }
    public string Message { get; set; }
}