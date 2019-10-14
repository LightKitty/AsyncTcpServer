using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTcpServer
{
    class ReadWriteObject
    {
        public TcpClient client;
        public NetworkStream netStream;
        public byte[] readBytes;
        public byte[] writeBytes;
        public ReadWriteObject(TcpClient client)
        {
            this.client = client;
            netStream = client.GetStream();
            readBytes = new byte[client.ReceiveBufferSize];
            writeBytes = new byte[client.SendBufferSize];
        }
        public void InitReadArray()
        {
            readBytes = new byte[client.ReceiveBufferSize];
        }
        public void InitWriteArray()
        {
            writeBytes = new byte[client.SendBufferSize];
        }
    }
}
