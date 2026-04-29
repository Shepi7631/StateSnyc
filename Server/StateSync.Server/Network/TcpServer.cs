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
                // 不 await：让每个客户端在独立任务中并发运行，主循环立即接受下一个连接
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
            // 每连接分配一次，循环复用，避免每个包都分配 8 字节
            byte[] headerBuf = new byte[8];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var (type, data, dataLength) = await PacketReader.ReadClientPacketAsync(stream, headerBuf);
                    try
                    {
                        // 根据客户端信息的type,data,dataLength 调用 MessageDispatcher 处理并生成回包
                        // response 就是要发送回客户端的字节数组
                        var (response, responseLength) = _dispatcher.Dispatch(type, data, dataLength);
                        try
                        {
                            await stream.WriteAsync(response.AsMemory(0, responseLength), ct);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(response);
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
        }
    }
}
