﻿//****************************************************************************
// Description:
// Author: hiramtan@qq.com
//***************************************************************************

//#define Thread




#if Thread
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace HiSocket.Tcp
{
    public class TcpClient : ISocket
    {
        public int TimeOut { get; set; }
        public int ReceiveBufferSize { get; set; }
        public Action<SocketState> StateEvent { get; set; }
        public bool IsConnected { get; }

        private System.Net.Sockets.TcpClient tcp;
        private IProto iPackage;
        public void Connect(string ip, int port)
        {
            throw new NotImplementedException();
        }

        public void DisConnect()
        {
            throw new NotImplementedException();
        }

        public void Send(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public long Ping()
        {
            throw new NotImplementedException();
        }


        private void InitThread()
        {
            Thread sendThread = new Thread(Send);
            sendThread.Start();


        }


        Queue<byte[]> sendQueue = new Queue<byte[]>();
        MemoryStream sendMS = new MemoryStream();
        private bool isSendThreadOn;
        private void Send()
        {
            while (isSendThreadOn)
            {
                if (!IsConnected)//主动or异常断开连接
                    break;

                lock (sendQueue)
                {
                    if (sendQueue.Count > 0)
                    {
                        var msg = sendQueue.Dequeue();
                        sendMS.Seek(0, SeekOrigin.End);
                        sendMS.Write(msg, 0, msg.Length);
                        sendMS.Seek(0, SeekOrigin.Begin);
                        iPackage.Pack(sendMS);
                        var toSend = sendMS.GetBuffer();
                        tcp.Client.BeginSend(toSend, 0, toSend.Length, SocketFlags.None, delegate (IAsyncResult ar)
                         {
                             try
                             {
                                 System.Net.Sockets.TcpClient tcp = ar.AsyncState as System.Net.Sockets.TcpClient;
                                 tcp.EndConnect(ar);
                             }
                             catch (Exception e)
                             {
                                 Console.WriteLine(e);
                                 throw;
                             }
                         }, tcp);
                    }
                }
            }
        }




    }
}



#else
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace HiSocket.Tcp
{
    public class TcpClient : ISocket
    {
        public int TimeOut
        {
            private get { return timeOut; }
            set { timeOut = value; }
        }

        public int ReceiveBufferSize
        {
            private get { return receiveBufferSize; }
            set
            {
                receiveBufferSize = value;
                m_ReceiveBuffer = new byte[ReceiveBufferSize];
            }
        }

        public Action<SocketState> StateEvent { get; set; }
        public bool IsConnected { get { return client != null && client.Client != null && client.Connected; } }


        private IPackage iPackage;
        private System.Net.Sockets.TcpClient client;
        private int receiveBufferSize = 1024 * 128;//128k
        private byte[] m_ReceiveBuffer;
        private int timeOut = 5000;//5s:收发超时时间

        public TcpClient(IPackage iPackage)
        {
            m_ReceiveBuffer = new byte[ReceiveBufferSize];
            this.iPackage = iPackage;
            client = new System.Net.Sockets.TcpClient();
            client.NoDelay = true;
            client.SendTimeout = client.ReceiveTimeout = TimeOut;
        }

        public void Connect(string ip, int port)
        {
            ChangeState(SocketState.Connecting);
            if (IsConnected)
            {
                ChangeState(SocketState.Connected);
                return;
            }
            try
            {
                this.client.BeginConnect(ip, port, (delegate (IAsyncResult ar)
                {
                    try
                    {
                        System.Net.Sockets.TcpClient tcp = ar.AsyncState as System.Net.Sockets.TcpClient;
                        tcp.EndConnect(ar);
                        if (tcp.Connected)
                        {
                            ChangeState(SocketState.Connected);
                            tcp.Client.BeginReceive(m_ReceiveBuffer, 0, m_ReceiveBuffer.Length, SocketFlags.None, Receive, tcp);
                        }
                        else ChangeState(SocketState.DisConnected);
                    }
                    catch (Exception e)
                    {
                        ChangeState(SocketState.DisConnected);
                        throw new Exception(e.ToString());
                    }
                }), client);
            }
            catch (Exception e)
            {
                ChangeState(SocketState.DisConnected);
                throw new Exception(e.ToString());
            }
        }

        private IByteArray iByteArray = new ByteArray();
        public void Send(byte[] bytes)
        {
            if (!IsConnected)
            {
                ChangeState(SocketState.DisConnected);
                throw new Exception("receive failed");
            }
            try
            {
                iPackage.Pack(bytes);
                client.Client.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, delegate (IAsyncResult ar)
                {
                    try
                    {
                        System.Net.Sockets.TcpClient tcp = ar.AsyncState as System.Net.Sockets.TcpClient;
                        tcp.Client.EndSend(ar);
                    }
                    catch (Exception e)
                    {
                        ChangeState(SocketState.DisConnected);
                        throw new Exception(e.ToString());
                    }
                }, client);
            }
            catch (Exception e)
            {
                ChangeState(SocketState.DisConnected);
                throw new Exception(e.ToString());
            }
        }


        void Receive(IAsyncResult ar)
        {
            if (!IsConnected)
            {
                ChangeState(SocketState.DisConnected);
                throw new Exception("receive failed");
            }
            try
            {
                System.Net.Sockets.TcpClient tcp = ar as System.Net.Sockets.TcpClient;
                int length = tcp.Client.EndReceive(ar);
                if (length > 0)
                {
                    byte[] bytes = new byte[length];




                    sendMS.Seek(0, SeekOrigin.End);
                    sendMS.Write(m_ReceiveBuffer, 0, length);
                    sendMS.Seek(0, SeekOrigin.Begin);
                    iPackage.Unpack(sendMS);
                }
                tcp.Client.BeginReceive(m_ReceiveBuffer, 0, m_ReceiveBuffer.Length, SocketFlags.None, Receive, tcp);
            }
            catch (Exception e)
            {
                ChangeState(SocketState.DisConnected);
                throw new Exception(e.ToString());
            }
        }

        public void DisConnect()
        {
            if (IsConnected)
            {
                //client.Client.Shutdown(SocketShutdown.Both);
                client.Close();//close already contain shutdown
                client = null;
            }
            ChangeState(SocketState.DisConnected);
            //StateEvent = null;
        }
        public long Ping()
        {
            System.Net.NetworkInformation.Ping tempPing = new System.Net.NetworkInformation.Ping();
            System.Net.NetworkInformation.PingReply temPingReply = tempPing.Send();
            return temPingReply.RoundtripTime;
        }


        private void ChangeState(SocketState state)
        {
            if (StateEvent != null)
            {
                StateEvent(state);
            }
        }
    }
}
#endif