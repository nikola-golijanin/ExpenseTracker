using TigerBeetle;

namespace ExpenseTracker;

public static class TigerBeetle
{
    private static readonly UInt128 ClusterId = UInt128.Zero;
    private const string DefaultTigerBeetleAddress = "3000";
    
    public static void Execute(Action<Client> operation)
    {
        using var client = CreateTigerBeetleClient();
        operation(client);
    }
    
    public static T Execute<T>(Func<Client, T> operation)
    {
        using var client = CreateTigerBeetleClient();
        return operation(client);
    }
    
    private static Client CreateTigerBeetleClient()
    {
        var tbAddress = Environment.GetEnvironmentVariable("TB_ADDRESS");
        var addresses = new[] { string.IsNullOrWhiteSpace(tbAddress) ? DefaultTigerBeetleAddress : tbAddress };
        return new Client(ClusterId, addresses);
    }
}