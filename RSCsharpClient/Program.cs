using System;

namespace RSCsharpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            UDPsocketClient socClient = new UDPsocketClient();
            socClient.run();
            //Console.WriteLine("Hello World!");
        }
    }
}
