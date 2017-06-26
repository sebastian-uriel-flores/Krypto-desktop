using FilesEncryptor.dto;
using FilesEncryptor.helpers;
using FilesEncryptor.helpers.processes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.utils
{
    public static class BitOps
    {
        public static BitCode Join(List<BitCode> codes, KryptoProcess processToWrite = null)
        {
            BitCode joined = BitCode.EMPTY;

            foreach(BitCode code in codes)
            {
                joined.Append(code);
            }

            return joined;
        }

        public static BitCode Xor(List<BitCode> codes)
        {
            BitCode result = BitCode.EMPTY;

            if (codes != null)
            {
                List<BitCode> bitsFirstElement = codes[0].Explode(1, false).Item1;

                //Opero de a pares de códigos
                for (int pos = 1; pos < codes.Count; pos++)
                {
                    List<BitCode> bitsSecondElement = codes[pos].Explode(1, false).Item1;
                    List<BitCode> xorBits = new List<BitCode>();

                    //Realizo el xor bit a bit,
                    //entre el bit 'i' del primer elemento y el bit 'i' del segundo elemento
                    for (int i = 0; i < bitsFirstElement.Count; i++)
                    {
                        byte xorRes = (byte)(bitsFirstElement[i].Code[0] ^ bitsSecondElement[i].Code[0]);
                        xorBits.Add(new BitCode(new List<byte>() { xorRes }, 1));
                    }

                    //El resultado pasará a ser el primer operandi del siguiente 'xor'
                    bitsFirstElement = xorBits;
                }
                result = Join(bitsFirstElement);
            }

            return result;
        }

        public static BitCode And(List<BitCode> codes)
        {
            BitCode result = BitCode.EMPTY;

            if(codes != null)
            {
                List<BitCode> bitsFirstElement = codes[0].Explode(1, false).Item1;

                //Opero de a pares de códigos
                for (int pos = 1; pos < codes.Count; pos++)
                {
                    List<BitCode> bitsSecondElement = codes[pos].Explode(1, false).Item1;
                    List<BitCode> andBits = new List<BitCode>();

                    //Determino cual de los elementos posee la menor cantidad de bits
                    //y realizo el And sobre esa cantidad de bits
                    int bitsCount = Math.Min(bitsFirstElement.Count, bitsSecondElement.Count);

                    //Realizo el 'and' bit a bit,
                    //entre el bit 'i' del primer elemento y el bit 'i' del segundo elemento
                    for(int i = 0; i < bitsCount; i++)
                    {
                        byte andRes = (byte)(bitsFirstElement[i].Code[0] & bitsSecondElement[i].Code[0]);
                        andBits.Add(new BitCode(new List<byte>() { andRes }, 1));
                    }

                    //El resultado pasará a ser el primer operandi del siguiente 'and'
                    bitsFirstElement = andBits;
                }
                result = Join(bitsFirstElement);
            }

            return result;
        }

        public static BitCode Or(List<BitCode> codes)
        {
            BitCode result = BitCode.EMPTY;

            if (codes != null)
            {
                List<BitCode> bitsFirstElement = codes[0].Explode(1, false).Item1;

                //Opero de a pares de códigos
                for (int pos = 1; pos < codes.Count; pos++)
                {
                    List<BitCode> bitsSecondElement = codes[pos].Explode(1, false).Item1;
                    List<BitCode> orBits = new List<BitCode>();

                    //Determino cual de los elementos posee la menor cantidad de bits
                    //y realizo el Or sobre esa cantidad de bits
                    int bitsCount = Math.Min(bitsFirstElement.Count, bitsSecondElement.Count);

                    //Realizo el 'or' bit a bit,
                    //entre el bit 'i' del primer elemento y el bit 'i' del segundo elemento
                    for (int i = 0; i < bitsCount; i++)
                    {
                        byte orRes = (byte)(bitsFirstElement[i].Code[0] | bitsSecondElement[i].Code[0]);
                        orBits.Add(new BitCode(new List<byte>() { orRes }, 1));
                    }

                    //El resultado pasará a ser el primer operandi del siguiente 'or'
                    bitsFirstElement = orBits;
                }
                result = Join(bitsFirstElement);
            }

            return result;
        }
    }
}
