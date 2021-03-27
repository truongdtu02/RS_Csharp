//RS(255, 223)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FEC
{
    class ReedSolomon
    {
        const int codewordleng = 255; //RS(255, 223)
        int msgleng = 223; //default
        int parleng = 32; //default

        int MAXDEG, MAXDEG2;

        byte[] codeword = new byte[codewordleng + 1];

        /* The Error Locator Polynomial, also known as Lambda or Sigma. Lambda[0] == 1 */
        int[] Lambda;

        /* The Error Evaluator Polynomial */
        int[] Omega;

        /* error locations found using Chien's search*/

        int[] gexp = new int[512];
        int[] glog = new int[256];

        /* erasure flags */
        int[] ErasureLocs = new int[256];
        int NErasures = 0;

        /* error flags */
        int[] ErrorLocs = new int[256];
        int NErrors;

        /* Encoder parity bytes */
        int[] pBytes;

        /* Decoder syndrome bytes */
        int[] synBytes;

        /* generator polynomial */
        int[] genPoly;

        public ReedSolomon( ) //no param
        {
            //msgleng = codewordleng - parleng;
            MAXDEG = (parleng * 2);
            MAXDEG2 = (parleng * 4);

            /* The Error Locator Polynomial, also known as Lambda or Sigma. Lambda[0] == 1 */
            Lambda = new int[MAXDEG];

            /* The Error Evaluator Polynomial */
            Omega = new int[MAXDEG];

            /* Encoder parity bytes */
            pBytes = new int[MAXDEG];

            /* Decoder syndrome bytes */
            synBytes = new int[MAXDEG];

            /* generator polynomial */
            genPoly = new int[MAXDEG2];

            /* Initialization the ECC library */
            initialize_ecc();
        }


        //constructor
        public ReedSolomon(int _parlen)
        {
            //check parlen
            if (_parlen < 1)  _parlen = 1;
            if (_parlen > 254) _parlen = 254;
            parleng = _parlen;
            msgleng = codewordleng - parleng;
            MAXDEG = (parleng * 2);
            MAXDEG2 = (parleng * 4);

            /* The Error Locator Polynomial, also known as Lambda or Sigma. Lambda[0] == 1 */
            Lambda = new int[MAXDEG];

            /* The Error Evaluator Polynomial */
            Omega = new int[MAXDEG];

            /* Encoder parity bytes */
            pBytes = new int[MAXDEG];

            /* Decoder syndrome bytes */
            synBytes = new int[MAXDEG];

            /* generator polynomial */
            genPoly = new int[MAXDEG2];

            /* Initialization the ECC library */
            initialize_ecc();
        }

        //return encode block length from data size
        public int GetEncodeBlockLeng(int _datalen)
        {
            if(_datalen % msgleng == 0)
                return (_datalen / msgleng) * parleng + _datalen;
            else
                return (_datalen / msgleng + 1) * parleng + _datalen;
            //ex _data = 500B, parleng = 32 -> msgleng = 255 - 32 = 223; CodewordLeng = 500B + 3*32
            //ex _data = 446B, parleng = 32 -> msgleng = 255 - 32 = 223; CodewordLeng = 446B + 2*32
        }

        //return data length from encode block length
        public int GetDataLeng(int _encodeblocklen)
        {
            if(_encodeblocklen % codewordleng == 0)
                return _encodeblocklen - (_encodeblocklen / codewordleng) * parleng;
            else
                return _encodeblocklen - (_encodeblocklen / codewordleng + 1) * parleng;
        }

        //return block code
        public int encode(byte[] _data, byte[] _encodeblock)
        {
            int _datalen = _data.Length;
            int _encodeblockleng = _encodeblock.Length;

            //check _datalen vs _encodeblockleng
            if (GetEncodeBlockLeng(_datalen) != _encodeblockleng) return -1;

            //split _data to some msgleng Byte
            int loop = _datalen / msgleng;
            byte[] msgtmp = new byte[msgleng];
            int oldByte = _datalen % msgleng; //check if have a block data < 255B

            for(int i = 0; i < loop; i++) {
                Buffer.BlockCopy(_data, i * msgleng, msgtmp, 0, msgleng);
                encode_data(msgtmp, msgleng, codeword);
                Buffer.BlockCopy(codeword, 0, _encodeblock, i * codewordleng, codewordleng);
            }
            if(oldByte != 0) {
                Buffer.BlockCopy(_data, loop * msgleng, msgtmp, 0, oldByte);
                encode_data(msgtmp, oldByte, codeword);
                Buffer.BlockCopy(codeword, 0, _encodeblock, loop * codewordleng, oldByte + parleng);
            }

            return 0;
        }

        public int decode(byte[] _receivedData, byte[] _decodedata) {
            int _lengthReceivedData = _receivedData.Length;
            int _lengthDecodeData = _decodedata.Length;

            if (_lengthDecodeData < GetDataLeng(_lengthReceivedData)) return -1;

            //split _data to some 255B
            int loop = _lengthReceivedData / codewordleng;
            int oldByte = _lengthReceivedData % codewordleng; //check if have a block data < 255B

            for (int i = 0; i < loop; i++)
            {
                Buffer.BlockCopy(_receivedData, i * codewordleng, codeword, 0, codewordleng);
                if(decode_data(codeword, codewordleng) == 0) return 0;
                Buffer.BlockCopy(codeword, 0, _decodedata, i * msgleng, msgleng);
            }
            if (oldByte > parleng)
            {
                Buffer.BlockCopy(_receivedData, loop * codewordleng, codeword, 0, oldByte);
                if (decode_data(codeword, oldByte) == 0) return 0;
                Buffer.BlockCopy(codeword, 0, _decodedata, loop * msgleng, oldByte - parleng);
            }

            return 1;
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


        //public void runReedSolomonTest()
        //{
        //for (int iter = 0; iter < looptime; iter++)
        //{
        //    //create random mesage
        //    GenRand(msg, messageLength);

        //    /* Encode data into codeword, adding parleng parity bytes */
        //    encode_data(msg, messageLength, codeword);

        //    //save code work to compare
        //    saveCodeWord();

        //    /* Add one error */
        //    bsc(codeword, berP, codewordleng);

        //    printerrors();

        //    decode_data(codeword, parleng);

        //    if (correctFEC() == 1) correcttime++;
        //}
        //Console.WriteLine("Frame done {0}", correcttime);
        //return correcttime;
        //}

        /* From  Cain, Clark, "Error-Correction Coding For Digital Communications", pp. 216. */
        void Modified_Berlekamp_Massey()
        {
            int n, L, L2, k, d, i;
            int[] psi = new int[MAXDEG];
            int[] psi2 = new int[MAXDEG];
            int[] D = new int[MAXDEG];
            int[] gamma = new int[MAXDEG];

            /* initialize Gamma, the erasure locator polynomial */
            init_gamma(gamma);

            /* initialize to z */
            copy_poly(D, gamma);
            mul_z_poly(D);

            copy_poly(psi, gamma);
            k = -1; L = NErasures;

            for (n = NErasures; n < parleng; n++)
            {

                d = compute_discrepancy(psi, synBytes, L, n);

                if (d != 0)
                {

                    /* psi2 = psi - d*D */
                    for (i = 0; i < MAXDEG; i++) psi2[i] = psi[i] ^ gmult(d, D[i]);


                    if (L < (n - k))
                    {
                        L2 = n - k;
                        k = n - L;
                        /* D = scale_poly(ginv(d), psi); */
                        for (i = 0; i < MAXDEG; i++) D[i] = gmult(psi[i], ginv(d));
                        L = L2;
                    }

                    /* psi = psi2 */
                    for (i = 0; i < MAXDEG; i++) psi[i] = psi2[i];
                }

                mul_z_poly(D);
            }

            for (i = 0; i < MAXDEG; i++) Lambda[i] = psi[i];
            compute_modified_omega();


        }

        /* given Psi (called Lambda in Modified_Berlekamp_Massey) and synBytes,
           compute the combined erasure/error evaluator polynomial as
           Psi*S mod z^4
          */
        void compute_modified_omega()
        {
            int i;
            int[] product = new int[MAXDEG2];

            mult_polys(product, Lambda, synBytes);
            zero_poly(Omega);
            for (i = 0; i < parleng; i++) Omega[i] = product[i];
        }

        /* polynomial multiplication */
        void mult_polys(int[] dst, int[] p1, int[] p2)
        {
            int i, j;
            int[] tmp1 = new int[MAXDEG2];

            for (i = 0; i < (MAXDEG2); i++) dst[i] = 0;

            for (i = 0; i < MAXDEG; i++)
            {
                for (j = MAXDEG; j < (MAXDEG2); j++) tmp1[j] = 0;

                /* scale tmp1 by p1[i] */
                for (j = 0; j < MAXDEG; j++) tmp1[j] = gmult(p2[j], p1[i]);
                /* and mult (shift) tmp1 right by i */
                for (j = (MAXDEG2) - 1; j >= i; j--) tmp1[j] = tmp1[j - i];
                for (j = 0; j < i; j++) tmp1[j] = 0;

                /* add into partial product */
                for (j = 0; j < (MAXDEG2); j++) dst[j] ^= tmp1[j];
            }
        }



        /* gamma = product (1-z*a^Ij) for erasure locs Ij */
        void init_gamma(int[] gamma)
        {
            int e;
            int[] tmp = new int[MAXDEG];

            zero_poly(gamma);
            zero_poly(tmp);
            gamma[0] = 1;

            for (e = 0; e < NErasures; e++)
            {
                copy_poly(tmp, gamma);
                scale_poly(gexp[ErasureLocs[e]], tmp);
                mul_z_poly(tmp);
                add_polys(gamma, tmp);
            }
        }



        void compute_next_omega(int d, int[] A, int[] dst, int[] src)
        {
            int i;
            for (i = 0; i < MAXDEG; i++)
            {
                dst[i] = src[i] ^ gmult(d, A[i]);
            }
        }

        int compute_discrepancy(int[] lambda, int[] S, int L, int n)
        {
            int i, sum = 0;

            for (i = 0; i <= L; i++)
                sum ^= gmult(lambda[i], S[n - i]);
            return (sum);
        }

        /********** polynomial arithmetic *******************/

        void add_polys(int[] dst, int[] src)
        {
            int i;
            for (i = 0; i < MAXDEG; i++) dst[i] ^= src[i];
        }

        void copy_poly(int[] dst, int[] src)
        {
            int i;
            for (i = 0; i < MAXDEG; i++) dst[i] = src[i];
        }

        void scale_poly(int k, int[] poly)
        {
            int i;
            for (i = 0; i < MAXDEG; i++) poly[i] = gmult(k, poly[i]);
        }


        void zero_poly(int[] poly)
        {
            int i;
            for (i = 0; i < MAXDEG; i++) poly[i] = 0;
        }


        /* multiply by z, i.e., shift right by 1 */
        void mul_z_poly(int[] src)
        {
            int i;
            for (i = MAXDEG - 1; i > 0; i--) src[i] = src[i - 1];
            src[0] = 0;
        }


        /* Finds all the roots of an error-locator polynomial with coefficients
         * Lambda[j] by evaluating Lambda at successive values of alpha.
         *
         * This can be tested with the decoder's equations case.
         */


        void Find_Roots()
        {
            int sum, r, k;
            NErrors = 0;

            for (r = 1; r < 256; r++)
            {
                sum = 0;
                /* evaluate lambda at r */
                for (k = 0; k < parleng + 1; k++)
                {
                    sum ^= gmult(gexp[(k * r) % 255], Lambda[k]);
                }
                if (sum == 0)
                {
                    ErrorLocs[NErrors] = (255 - r); NErrors++;
                    //if (DEBUG) fprintf(stderr, "Root found at r = %d, (255-r) = %d\n", r, (255 - r));
                }
            }
        }

        /* Combined Erasure And Error Magnitude Computation
         *
         * Pass in the codeword, its size in bytes, as well as
         * an array of any known erasure locations, along the number
         * of these erasures.
         *
         * Evaluate Omega(actually Psi)/Lambda' at the roots
         * alpha^(-i) for error locs i.
         *
         * Returns 1 if everything ok, or 0 if an out-of-bounds error is found
         *
         */

        int correct_errors_erasures(byte[] _codeword, int csize)
        {
            int r, i, j, err;

            /* If you want to take advantage of erasure correction, be sure to
               set NErasures and ErasureLocs[] with the locations of erasures. 
               */
            NErasures = 0;
            //for (i = 0; i < NErasures; i++) ErasureLocs[i] = erasures[i];

            Modified_Berlekamp_Massey();
            Find_Roots();


            if ((NErrors <= parleng) && NErrors > 0)
            {

                /* first check for illegal error locs */
                for (r = 0; r < NErrors; r++)
                {
                    if (ErrorLocs[r] >= csize)
                    {
                        //if (DEBUG) fprintf(stderr, "Error loc i=%d outside of codeword length %d\n", i, csize);
                        return (0);
                    }
                }

                for (r = 0; r < NErrors; r++)
                {
                    int num, denom;
                    i = ErrorLocs[r];
                    /* evaluate Omega at alpha^(-i) */

                    num = 0;
                    for (j = 0; j < MAXDEG; j++)
                        num ^= gmult(Omega[j], gexp[((255 - i) * j) % 255]);

                    /* evaluate Lambda' (derivative) at alpha^(-i) ; all odd powers disappear */
                    denom = 0;
                    for (j = 1; j < MAXDEG; j += 2)
                    {
                        denom ^= gmult(Lambda[j], gexp[((255 - i) * (j - 1)) % 255]);
                    }

                    err = gmult(num, ginv(denom));
                    //if (DEBUG) fprintf(stderr, "Error magnitude %#x at loc %d\n", err, csize - i);

                    _codeword[csize - i - 1] ^= (byte)err;
                }
                return (1);
            }
            else
            {
                //if (DEBUG && NErrors) fprintf(stderr, "Uncorrectable codeword\n");
                return (0);
            }
        }

        /* Computes the CRC-CCITT checksum on array of byte data, length len
        */
        UInt16 crc_ccitt(byte[] msg, int len)
        {
            int i;
            UInt16 acc = 0;

            for (i = 0; i < len; i++)
            {
                acc = crchware((UInt16)msg[i], (UInt16)0x1021, acc);
            }
            return (acc);
        }

        /* models crc hardware (minor variation on polynomial division algorithm) */
        UInt16 crchware(UInt16 data, UInt16 genpoly, UInt16 accum)
        {
            //static UInt16 i;
            data <<= 8;
            for (int i = 8; i > 0; i--)
            {
                if (((data ^ accum) & 0x8000) > 0)
                    accum = (UInt16)(((accum << 1) ^ genpoly) & 0xFFFF);
                else
                    accum = (UInt16)((accum << 1) & 0xFFFF);
                data = (UInt16)((data << 1) & 0xFFFF);
            }
            return (accum);
        }

        /* This is one of 14 irreducible polynomials
         * of degree 8 and cycle length 255. (Ch 5, pp. 275, Magnetic Recording)
         * The high order 1 bit is implicit */
        /* x^8 + x^4 + x^3 + x^2 + 1 */

        void init_galois_tables()
        {
            /* initialize the table of powers of alpha */
            init_exp_table();
        }


        void init_exp_table()
        {
            int i, z;
            int pinit, p1, p2, p3, p4, p5, p6, p7, p8;

            pinit = p2 = p3 = p4 = p5 = p6 = p7 = p8 = 0;
            p1 = 1;

            gexp[0] = 1;
            gexp[255] = gexp[0];
            glog[0] = 0;            /* shouldn't log[0] be an error? */

            for (i = 1; i < 256; i++)
            {
                pinit = p8;
                p8 = p7;
                p7 = p6;
                p6 = p5;
                p5 = p4 ^ pinit;
                p4 = p3 ^ pinit;
                p3 = p2 ^ pinit;
                p2 = p1;
                p1 = pinit;
                //gexp[i] = p1 + p2 * 2  +  p3 * 4 + p4 * 8  + p5 * 16 + p6 * 32 + p7 * 64 + p8 * 128;
                gexp[i] = p1 + (p2 << 1) + (p3 << 2) + (p4 << 3) + (p5 << 4) + (p6 << 5) + (p7 << 6) + (p8 << 7);
                gexp[i + 255] = gexp[i];
            }

            for (i = 1; i < 256; i++)
            {
                for (z = 0; z < 256; z++)
                {
                    if (gexp[z] == i)
                    {
                        glog[i] = z;
                        break;
                    }
                }
            }
        }

        /* multiplication using logarithms */
        int gmult(int a, int b)
        {
            int i, j;
            if (a == 0 || b == 0) return (0);
            i = glog[a];
            j = glog[b];
            return (gexp[i + j]);
        }


        int ginv(int elt)
        {
            return (gexp[255 - glog[elt]]);
        }

        /* Initialize lookup tables, polynomials, etc. */
        void initialize_ecc()
        {
            /* Initialize the galois field arithmetic tables */
            init_galois_tables();

            /* Compute the encoder generator polynomial */
            compute_genpoly(parleng, genPoly);
        }

        void zero_fill_from(byte[] buf, int from, int to)
        {
            int i;
            for (i = from; i < to; i++) buf[i] = 0;
        }


        /* Append the parity bytes onto the end of the message */
        void build_codeword(byte[] msg, int nbytes, byte[] dst)
        {
            int i;

            for (i = 0; i < nbytes; i++) dst[i] = msg[i];

            for (i = 0; i < parleng; i++)
            {
                dst[i + nbytes] = (byte)pBytes[parleng - 1 - i];
            }
        }

        /**********************************************************
         * Reed Solomon Decoder
         *
         * Computes the syndrome of a codeword. Puts the results
         * into the synBytes[] array.
         */

        int decode_data(byte[] data, int nbytes)
        {
            int i, j, sum;
            if (nbytes > data.Length) return 0;
            for (j = 0; j < parleng; j++)
            {
                sum = 0;
                for (i = 0; i < nbytes; i++)
                {
                    sum = data[i] ^ gmult(gexp[j + 1], sum);
                }
                synBytes[j] = sum;
            }
            if (check_syndrome() != 0)
            {
                return correct_errors_erasures(data, nbytes);
            }
            return 0;
        }

        /* Check if the syndrome is zero */
        int check_syndrome()
        {
            int i, nz = 0;
            for (i = 0; i < parleng; i++)
            {
                if (synBytes[i] != 0)
                {
                    nz = 1;
                    break;
                }
            }
            return nz;
        }


        /* Create a generator polynomial for an n byte RS code.
         * The coefficients are returned in the genPoly arg.
         * Make sure that the genPoly array which is passed in is
         * at least n+1 bytes long.
         */

        void compute_genpoly(int nbytes, int[] genpoly)
        {
            int[] tp = new int[256];
            int[] tp1 = new int[256];

            /* multiply (x + a^n) for n = 1 to nbytes */

            zero_poly(tp1);
            tp1[0] = 1;

            for (int i = 1; i <= nbytes; i++)
            {
                zero_poly(tp);
                tp[0] = gexp[i];        /* set up x+a^n */
                tp[1] = 1;

                mult_polys(genpoly, tp, tp1);
                copy_poly(tp1, genpoly);
            }
        }

        /* Simulate a LFSR with generator polynomial for n byte RS code.
         * Pass in a pointer to the data array, and amount of data.
         *
         * The parity bytes are deposited into pBytes[], and the whole message
         * and parity are copied to dest to make a codeword.
         *
         */

        void encode_data(byte[] msg, int nbytes, byte[] dst)
        {
            int i, dbyte, j;
            int[] LFSR = new int[parleng + 1];

            for (i = 0; i < parleng + 1; i++) LFSR[i] = 0;

            for (i = 0; i < nbytes; i++)
            {
                dbyte = msg[i] ^ LFSR[parleng - 1];
                for (j = parleng - 1; j > 0; j--)
                {
                    LFSR[j] = LFSR[j - 1] ^ gmult(genPoly[j], dbyte);
                }
                LFSR[0] = gmult(genPoly[0], dbyte);
            }

            for (i = 0; i < parleng; i++)
                pBytes[i] = LFSR[i];

            build_codeword(msg, nbytes, dst);
        }
    }
}
