using System;
using System.Collections.Generic;
using FEC;

namespace RS_serverCsharp
{
    class Program
    {
        public static List<client_IPEndPoint> clientList;
        static void Main(string[] args)
        {
            ReedSolomon _rs = new ReedSolomon(); //initialize parity bytes
            int encodelength = 1472; //Byteencodelength
            int dataleng = _rs.GetDataLeng(encodelength);
            int[] dataInt = new int[dataleng / 4];
            //initialize
            for(int i = 0; i < dataInt.Length; i++) {
                dataInt[i] = i;
            }

            byte[] data = new byte[dataInt.Length * sizeof(int)];
            Buffer.BlockCopy(dataInt, 0, data, 0, data.Length);

            byte[] encode = new byte[encodelength];

            //_rs.encode(data, encode);

            ////add error
            //Random _random = new Random();
            //for(int i = 0; i < 29; i++) {
            //    encode[_random.Next(encode.Length - 1)] ^= (byte)_random.Next();
            //}

            ////decode
            //byte[] decodedata = new byte[data.Length];
            //int errorDecode = _rs.decode(encode, decodedata);

            ////check correct decode
            //for(int i = 0; i < dataleng; i++) { 
            //    if(data[i] != decodedata[i]) {
            //        Console.WriteLine("error");
            //        break;
            //    }
            //}

            clientList = new List<client_IPEndPoint>()
            {
                 new client_IPEndPoint(){ ID_client = "20154023", On = true, NumSend = 1},
                 new client_IPEndPoint(){ ID_client = "00000001", On = true}
            };
            UDPsocket udpsocket = new UDPsocket(clientList, encode, data);
            udpsocket.UDPsocketSend();
            //Console.WriteLine("Hello World!");
        }
    }
}
