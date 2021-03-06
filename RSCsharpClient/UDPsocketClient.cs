using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FEC;

namespace RSCsharpClient
{
    class UDPsocketClient
    {
        // chuyển đổi chuỗi ký tự thành object thuộc kiểu IPAddress
        //static IPAddress serverIp = IPAddress.Parse("45.118.145.137");
        static IPAddress serverIp = IPAddress.Parse("127.0.0.1"); //local host
        // chuyển chuỗi ký tự thành biến kiểu int
        static int serverPort = int.Parse("1308");

        // đây là "địa chỉ" của tiến trình server trên mạng
        // mỗi endpoint chứa ip của host và port của tiến trình
        IPEndPoint serverEndpoint = new IPEndPoint(serverIp, serverPort);

        const int size = 1600; // kích thước của bộ đệm
        byte[] receiveBuffer = new byte[size]; // mảng byte làm bộ đệm

        // khởi tạo object của lớp socket để sử dụng dịch vụ Udp
        // lưu ý SocketType của Udp là Dgram (datagram) 
        Socket socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

        byte[] sendBuffer = new byte[8];

        Thread threadRequest, threadReceive;

        public UDPsocketClient()
        {
            Console.Write("Nhap ID: ");
            string ID = Console.ReadLine();
            string newID = ID;
            int IDlengthOriginal = ID.Length;
            for (int i = 0; i < (8 - IDlengthOriginal); i++)
            {
                newID = "0" + newID;
            }
            sendBuffer = Encoding.ASCII.GetBytes(newID);

            //need at least one send to receive (Bind)
            socket.SendTo(sendBuffer, serverEndpoint);

            threadRequest = new Thread(() =>
            {
                request();
            });

            threadReceive = new Thread(() =>
            {
                receive();
            });

            //
        }

        public void run() {
            threadRequest.Start();
            threadReceive.Start();
        }

        void request()
        {
            while (true)
            {
                // gửi request đến server
                socket.SendTo(sendBuffer, serverEndpoint);
                Thread.Sleep(2000);
            }
        }

        //check bit diff
        int checkBitDiff(byte[] a, byte[] b, int leng) {
            int errorbits = 0, i, j;
            for (i = 0; i < leng; i++)
            {
                int diff = a[i] ^ b[i];
                for (j = 0; j < 8; j++)
                {
                    if ((diff & 1) == 1)
                        errorbits++;
                    diff = diff >> 1;
                }
            }
            return errorbits;
        }

        void receive()
        {
            EndPoint dummyEndpoint = new IPEndPoint(IPAddress.Any, 0);
            ReedSolomon rs = new ReedSolomon();

            //check error bits
            int encodelength = 1472; //Byteencodelength
            byte[] _encode = new byte[encodelength];

            {
                int dataleng = rs.GetDataLeng(encodelength);
                int[] dataInt = new int[dataleng / 4];
                //initialize
                for (int i = 0; i < dataInt.Length; i++)
                {
                    dataInt[i] = i;
                }

                byte[] datarand = new byte[dataInt.Length * sizeof(int)];
                Buffer.BlockCopy(dataInt, 0, datarand, 0, datarand.Length);
                rs.encode(datarand, _encode);
            }
            
            int Frame = 0, error = 0, lost = 0;
            int maxError = 0, minError = 100000;

            while (true)
            {
                try
                {
                    int length = socket.ReceiveFrom(receiveBuffer, ref dummyEndpoint);
                    //Frame++;
                    int res = checkBitDiff(_encode, receiveBuffer, length);
                    maxError = (res > maxError) ? res : maxError;
                    minError = (res < minError) ? res : minError;

                    int decodedataleng = rs.GetDataLeng(length);
                    byte[] decodedata = new byte[decodedataleng];
                    int sign = rs.decode(receiveBuffer, length, decodedata);
                    if(sign == 0) { //can't correct
                        error++;
                    } else {
                        int[] data = new int[decodedata.Length / 4];
                        Buffer.BlockCopy(decodedata, 0, data, 0, decodedata.Length);
                        //check equal
                        //for(int i = 1; i < data.Length; i++) { 
                        //    if(data[i] != i) {
                        //        error++;
                        //        break;
                        //    }
                        //}
                        //check order
                        int order = BitConverter.ToInt32(decodedata, 0);
                        if (order > Frame)
                        {
                            lost += order - Frame - 1;
                            Frame = order;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //Console.WriteLine(ex);
                }
                if ((Frame % 50 == 0) && (Frame > 0))
                    //Console.WriteLine("Frame {0} . Error: {1} . Error bit: Max {2}, Min: {3}", Frame, error, maxError, minError);
                    Console.WriteLine("Frame {0} . Lost: {1} ", Frame, lost);

            }
        }

    }
}
