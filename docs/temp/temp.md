private static void DumpJwt(string jwt)
{
    var parts = jwt.Split('.');
    if (parts.Length < 2)
    {
        Console.WriteLine("Not a JWT");
        return;
    }

    string Decode(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";

        s = s.Replace('-', '+').Replace('_', '/');

        switch (s.Length % 4)
        {
            case 0:
                break;
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
            default:
                // invalid base64 length
                return "";
        }

        try
        {
            var bytes = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }

    var payloadJson = Decode(parts[1]);
    Console.WriteLine(payloadJson);
}
