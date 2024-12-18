using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection.PortableExecutable;
using System.Text;

namespace PersistenceServer
{
    public class UserConnection
    {        
        private readonly MmoWsServer _mmoWsServer;
        private readonly WebSocket _webSocket;
        public Guid Id { get; private set; }
        public string cookie { get; set;  }

        // The maximum allowable message size (e.g., 1 MB)
        const long MaxMessageSize = 1 * 1024 * 1024; // 1 MB

        public UserConnection(MmoWsServer server, WebSocket webSocket)
        {
            _mmoWsServer = server;
            _webSocket = webSocket;
            Id = Guid.NewGuid();
            cookie = "";
        }

        public async Task HandleConnectionAsync()
        {
            // Notify server that a connection was established.
            InvokeOnConnected();

            // 2 KB buffer, to accomodate roughly 500 characters in length
            // if the message exceeds the buffer, it'll be accumulated until it's fully received
            // if it exceeds maximum size during accumulation, the user gets disconnected
            var buffer = new ArraySegment<byte>(new byte[2048]);
            MemoryStream messageStream = new MemoryStream();

            try
            {
                while (_webSocket.State == WebSocketState.Open)
                {
                    // when ReceiveAsync has nothing to receive, this thread is returned to the thread pool and is free to handle other incoming requests or do other work
                    var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    if (buffer.Array != null)
                    {
                        messageStream.Write(buffer.Array, buffer.Offset, result.Count);

                        // Check that the accumulated message size doesn't exceed the maximum allowed size
                        if (messageStream.Length > MaxMessageSize)
                        {
                            Console.WriteLine("Client sent a message exceeding the maximum allowable size. Disconnecting client.");
                            InvokeOnDisconnected();
                            await _webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Message size exceeded", CancellationToken.None);
                            messageStream.SetLength(0); // Clear the stream
                            return;
                        }
                    }
                    // If we have received the end of the message, process it, otherwise keep accumulating it
                    if (result.EndOfMessage)
                    { 
                        switch (result.MessageType)
                        {
                        /* TEXT MESSAGE */
                        case WebSocketMessageType.Text:
                        Console.WriteLine("Text messages aren't handled");
                        messageStream.SetLength(0);  // Clear the stream
                        break;

                        /* BINARY MESSAGE */
                        case WebSocketMessageType.Binary:
                        messageStream.Position = 0;  // Reset the stream position
                        BinaryReader reader = new(messageStream, Encoding.ASCII);

                        while (reader.PeekChar() > 0) // -1 is "end of stream", "0" is "undefined"
                        {
                            RpcType rpcPrefix = (RpcType)reader.ReadByte();
                            _mmoWsServer.InvokeOnMessageReceived(rpcPrefix, this, reader);
                        }
                        messageStream.SetLength(0);  // Clear the stream for the next message
                        break;

                        /* CLOSE MESSAGE */
                        case WebSocketMessageType.Close:
                        InvokeOnDisconnected();
                        if (result.CloseStatus.HasValue)
                        {
                            await _webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription ?? string.Empty, CancellationToken.None);
                        }
                        else
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                        }
                        messageStream.SetLength(0);  // Clear the stream
                        break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle potential exceptions during WebSocket communication.
                Console.WriteLine($"WebSocket session caught an error: {ex.Message}");
                InvokeOnDisconnected();
            }
        }

        public void Send(byte[] binaryMsg)
        {
            _webSocket.SendAsync(binaryMsg, WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        private void InvokeOnConnected()
        {
            BinaryReader reader = new(new MemoryStream(Array.Empty<byte>()));
            _mmoWsServer.InvokeOnMessageReceived(RpcType.RpcConnected, this, reader);
        }

        private void InvokeOnDisconnected()
        {
            BinaryReader reader = new(new MemoryStream(Array.Empty<byte>()));
            _mmoWsServer.InvokeOnMessageReceived(RpcType.RpcDisconnected, this, reader);
        }

        public async Task Disconnect()
        {
            InvokeOnDisconnected();
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
        }
    }
}
