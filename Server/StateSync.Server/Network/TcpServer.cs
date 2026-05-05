namespace StateSync.Server.Network;

using System.Buffers;
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
            var session = new ClientSession(stream);
            byte[] headerBuf = new byte[8];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, data, dataLength) = await PacketReader.ReadClientPacketAsync(stream, headerBuf);
                    try
                    {
                        var (response, responseLength) = _dispatcher.Dispatch(type, data, dataLength, session);
                        if (response != null)
                        {
                            try
                            {
                                await stream.WriteAsync(response.AsMemory(0, responseLength), ct);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(response);
                            }
                        }
                    }
                    finally
                    {
                        if (dataLength > 0) ArrayPool<byte>.Shared.Return(data);
                    }
                }
            }
            catch (EndOfStreamException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine($"Client error: {ex.Message}"); }
            finally
            {
                session.Room?.RemoveSession(session);
            }
        }
    }
}
