using System.Collections;
using System.Text;
using Newtonsoft.Json;

namespace PersistenceServer
{
    abstract public class BaseRpc
    {
        public RpcType RpcType = RpcType.RpcUndef;
        protected MmoWsServer? Server;

        public void SubscribeToMessages(MmoWsServer inServer)
        {
            Server = inServer;
            inServer.OnMessageReceived += TryTrigger;
        }

        private void TryTrigger(RpcType inRpcType, UserConnection conn, BinaryReader reader)
        {
            if (RpcType == inRpcType)
            {
                ReadRpc(conn, reader);
            }
        }

        // Override in subclasses: read from the reader, then add an action to server.Processor.ConQ
        // E.g.: server.processor.ConQ.Enqueue(() => Console.WriteLine("ah"));
        protected virtual void ReadRpc(UserConnection connection, BinaryReader reader) { }

        /*
        * Technical functions that help serialize messages
        */

        public static byte[] MergeByteArrays(params object[] list)
        {
            int totalBytesLength = 0;
            for (int i = 0; i < list.Length; i++)
                totalBytesLength += ((byte[])list[i]).Length;

            byte[] result = new byte[totalBytesLength];
            int pos = 0;
            for (int i = 0; i < list.Length; i++)
            {
                byte[] thisArray = (byte[])list[i];
                thisArray.CopyTo(result, pos);
                pos += thisArray.Length;
            }
            return result;
        }

        public static byte[] ToBytes(RpcType rpc)
        {
            return new[] { (byte)rpc };
        }

        // Encodes a string into a binary array and prefixes it with an integer for string length
        // So for example Hello will look as follows:
        // 00000000 00000000 00000000 00000101 (which is 5, the number of bytes in 'Hello')
        // 01001000 01100101 01101100 01101100 01101111 (which is 'Hello' itself)
        public static byte[] WriteMmoString(string str)
        {
            return MergeByteArrays(ToBytes(Encoding.UTF8.GetBytes(str).Length), Encoding.UTF8.GetBytes(str));
        }

        public static byte[] ToBytes(int num)
        {
            byte[] bytes = BitConverter.GetBytes(num);
            return bytes;
        }

        public static byte[] ToBytes(int[] intArray)
        {
            byte[] result = new byte[intArray.Length * sizeof(int)];
            Buffer.BlockCopy(intArray, 0, result, 0, result.Length);
            return result;
        }

        public static byte[] ToBytes(float num)
        {
            byte[] bytes = BitConverter.GetBytes(num);
            if (!BitConverter.IsLittleEndian)
            {
                bytes = bytes.Reverse().ToArray();
            }
            return bytes;
        }

        public static byte[] ToBytes(bool b)
        {
            return BitConverter.GetBytes(b);
        }

        protected static string GetGuildMembersJson(Guild guild)
        {
            return JsonConvert.SerializeObject(new GuildJson(guild.GetAllMembers()));
        }
    }
}
