using System.Net.Sockets;

namespace PaymentGateway.Api.Tests.BankSimulator;

public sealed class BankSimulatorFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> IsRunning = new(() =>
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("localhost", 8080).Wait(TimeSpan.FromSeconds(1)) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    });

    public BankSimulatorFactAttribute()
    {
        if (!IsRunning.Value)
        {
            Skip = "The bank simulator is not running. Start it with 'docker compose up -d'.";
        }
    }
}
