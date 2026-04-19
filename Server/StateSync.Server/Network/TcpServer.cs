namespace StateSync.Server.Network;

using System.Net;
using System.Net.Sockets;

public class TcpServer
{
    private readonly TcpListener _listener;
    private readonly MessageDispatcher _dispatcher;

    public TcpServer(int port, MessageDispatcher dispatcher)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _dispatcher = dispatcher;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener.Start();
        Console.WriteLine($"Listening on port {((IPEndPoint)_listener.LocalEndpoint).Port}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, data) = await PacketReader.ReadClientPacketAsync(stream);
                    byte[] response = _dispatcher.Dispatch(type, data);
                    await stream.WriteAsync(response, ct);
                }
            }
            catch (EndOfStreamException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"Client error: {ex.Message}"); }
        }
    }
}
