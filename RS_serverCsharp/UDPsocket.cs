using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using System.IO;
using FEC;

namespace RS_serverCsharp
{
    class UDPsocket
    {
        Thread threadListen, threadSend, threadCheckRequest;

        //socket UDP
        static IPAddress localIp = IPAddress.Any;
        static int localPort = 1308;
        IPEndPoint localEndPoint = new IPEndPoint(localIp, localPort);
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        //for threadListen
        byte[] receive_buffer = new byte[8];
        // biến này về sau sẽ chứa địa chỉ của tiến trình client nào gửi gói tin tới
        EndPoint receive_IPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        //List save client request
        List<client_IPEndPoint> clientList = new List<client_IPEndPoint>();
        public List<client_IPEndPoint> ClientList { get => clientList; set => clientList = value; }

        //for threadSend
        const int Max_send_buff_length = 1472;//1472; //534

        byte[] sendBuffer;
        byte[] oridata;

        ReedSolomon rs = new ReedSolomon(); //initialize parity bytes

        public UDPsocket(List<client_IPEndPoint> _clientList, byte[] _senddata, byte[] _oridata)
        {
            clientList = _clientList;
            if (_senddata.Length <= Max_send_buff_length) sendBuffer = _senddata;
            oridata = _oridata;

            try
            {
                socket.Bind(localEndPoint);
                Console.WriteLine($"Local socket bind to {localEndPoint}. Waiting for request ...");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void UDPsocketListen()
        {
            Stopwatch watch_client = new Stopwatch();
            watch_client.Start();

            threadListen = new Thread(() =>
            {
                threadListenFunc(watch_client);
            });

            threadCheckRequest = new Thread(() =>
            {
                threadCheckRequestFunc(watch_client);
            });

            threadCheckRequest.Priority = ThreadPriority.Lowest;
            threadListen.Priority = ThreadPriority.Normal;

            threadListen.Start();
            threadCheckRequest.Start();
        }

        public void UDPsocketSend()
        {
            UDPsocketListen();

            threadSend = new Thread(() =>
            {
                threadSendFunc();
            });
            threadSend.Priority = ThreadPriority.Highest;
            threadSend.Start();
        }
        private void analyzeRequest(int length, Stopwatch _watchClient)
        {
            if (length >= 8)
            {
                //get id client
                var ID_client_received = Encoding.ASCII.GetString(receive_buffer, 0, 8);

                //check client in List
                for (int i = 0; i < ClientList.Count; i++)
                {
                    if (String.Equals(ID_client_received, ClientList[i].ID_client))
                    {
                        ClientList[i].TimeStamp_ms = _watchClient.ElapsedMilliseconds; //update time request
                        ClientList[i].TimeOut = false;
                        ClientList[i].IPEndPoint_client = receive_IPEndPoint; //update IP, port UDP of client
                    }
                }
            }
        }

        private void threadListenFunc(Stopwatch _watchClient)
        {
            while (true)
            {
                //client need to send ID, string 4 byte
                int length = 0;
                try
                {
                    length = socket.ReceiveFrom(receive_buffer, ref receive_IPEndPoint);
                }
                catch//(Exception ex)
                {
                    //Console.WriteLine(ex);
                    continue;
                }

                analyzeRequest(length, _watchClient);
            }
        }

        private void threadCheckRequestFunc(Stopwatch _watchClient)
        {
            while (true)
            {
                for (int i = 0; i < ClientList.Count; i++)
                {
                    double offsetTime = _watchClient.ElapsedMilliseconds - ClientList[i].TimeStamp_ms;
                    if (offsetTime > 5000) // > 5s
                    {
                        ClientList[i].TimeOut = true;
                    }
                }
                Thread.Sleep(5000); //check every 5s
            }
        }

        private void threadSendFunc()
        {
            SocketFlags socketFlag = new SocketFlags();
            int numOfFrame = 0;
            byte[] tmp_byte = new byte[4];

            rs.encode(oridata, sendBuffer);
            while (true)
            {
                for (int i = 0; i < clientList.Count; i++)
                {
                    if ((!clientList[i].TimeOut) && (clientList[i].On))
                    {
                        try
                        {
                            socket.SendTo(sendBuffer, sendBuffer.Length, socketFlag, clientList[i].IPEndPoint_client);
                            numOfFrame++;
                            tmp_byte = BitConverter.GetBytes(numOfFrame);
                            Buffer.BlockCopy(tmp_byte, 0, oridata, 0, 4);
                            rs.encode(oridata, sendBuffer);
                        }
                        catch (Exception ex)
                        {
                            //Console.WriteLine(ex);
                        }
                    }
                }
                
                if ((numOfFrame % 60 == 0) && (numOfFrame > 0))
                        Console.WriteLine("Frame: {0}", numOfFrame); //every 2.7s
                Thread.Sleep(40); //90 ms
            }          
        }

    }

    class client_IPEndPoint
    {
        // biến này về sau sẽ chứa địa chỉ của tiến trình client nào gửi gói tin tới
        EndPoint ipEndPoint_client = new IPEndPoint(IPAddress.Any, 0);

        string id_client;

        public EndPoint IPEndPoint_client { get => ipEndPoint_client; set => ipEndPoint_client = value; }
        public string ID_client { get => id_client; set => id_client = value; }

        double timeStamp_ms = 0;

        bool timeOut = true; //timeOut = true, that mean don't receive request in last 5s, and don't send

        bool on; //change this on app

        int numSend = 1; //multi packet is sent to client to improve UDP loss
        public int NumSend { get => numSend; set => numSend = value; }

        //server just sends to client when timeOut == false and On == true

        public double TimeStamp_ms { get => timeStamp_ms; set => timeStamp_ms = value; }
        public bool TimeOut { get => timeOut; set => timeOut = value; }
        public bool On { get => on; set => on = value; }
    }

    class soundTrack
    {
        string filePath;
        int duration_ms = 0; //duration of a sound Track
        int playingTime_ms = 0; //current time playing of sound track

        public string FilePath { get => filePath; set => filePath = value; }
        public int Duration_ms { get => duration_ms; }
        public int PlayingTime_ms { get => playingTime_ms; }
    }

    class headerPacket
    {
        ////header of UDP packet
        //1-byte: volume, 1-byte: ID_song, 2-byte: totalLength
        //4-byte: ID_frame
        //2-byte: numOfFrame, 2-byte checksum
        //de cho an toan, nen tinh checksum cho header nay

        //total byte in header
        UInt16 length = 14;
        byte volume = 0x00; // max:min 0x00:0xFE
        byte id_song;
        UInt16 totalLength;
        UInt32 id_frame;
        UInt16 numOffFrame;
        UInt16 checkSum;
        UInt16 checkSumData;

        public UInt16 Length { get => length; }

        internal byte IDsong { get => id_song; set => id_song = value; }
        internal ushort TotalLength { get => totalLength; set => totalLength = value; }
        internal uint IDframe { get => id_frame; set => id_frame = value; }
        internal ushort NumOffFrame { get => numOffFrame; set => numOffFrame = value; }

        internal byte Volume
        {
            get { return volume; }
            set
            {
                if (value == 0xFF)
                    volume = 0xFE;
                else
                    volume = value;
            }
        }

        internal void copyHeaderToBuffer(byte[] _buffer)
        {
            _buffer[0] = volume;
            _buffer[1] = id_song;
            byte[] tmp_byte = new byte[4];
            tmp_byte = BitConverter.GetBytes(totalLength);
            Buffer.BlockCopy(tmp_byte, 0, _buffer, 2, 2);
            tmp_byte = BitConverter.GetBytes(id_frame);
            Buffer.BlockCopy(tmp_byte, 0, _buffer, 4, 4);
            tmp_byte = BitConverter.GetBytes(numOffFrame);
            Buffer.BlockCopy(tmp_byte, 0, _buffer, 8, 2);

            //caculate checksum for header and checksum for data
            checkSum = caculateChecksum(_buffer, 0, length - 4); //header
            checkSumData = caculateChecksum(_buffer, length, totalLength - length);

            tmp_byte = BitConverter.GetBytes(checkSum);
            Buffer.BlockCopy(tmp_byte, 0, _buffer, 10, 2);
            tmp_byte = BitConverter.GetBytes(checkSumData);
            Buffer.BlockCopy(tmp_byte, 0, _buffer, 12, 2);
        }

        static UInt16 caculateChecksum(byte[] data, int offset, int length)
        {
            UInt32 checkSum = 0;
            int index = offset;
            while (length > 1)
            {
                checkSum += ((UInt32)data[index] << 8) | ((UInt32)data[index + 1]); //little edian
                length -= 2;
                index += 2;
            }
            if (length == 1) // still have 1 byte
            {
                checkSum += ((UInt32)data[index] << 8);
            }
            while ((checkSum >> 16) > 0) //checkSum > 0xFFFF
            {
                checkSum = (checkSum & 0xFFFF) + (checkSum >> 16);
            }
            //inverse
            checkSum = ~checkSum;
            return (UInt16)checkSum;
        }
    }
}
