using System;
using FEC;

namespace RS_Csharp
{
    class Program
    {
        //byte[] msg = new byte[223];
        static void Main(string[] args)
        {
            ReedSolomon _rs = new ReedSolomon();
            _rs.runReedSolomon();
            Console.WriteLine("Hello World!");
        }
    }
}
