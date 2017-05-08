using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FilesEncryptor.utils;

namespace FilesEncryptor.dto
{
    public class BitCode
    {
        #region CONSTS

        public static BitCode EMPTY => new BitCode(new List<byte> (), 0).Copy();

        public static BitCode ZERO => new BitCode(new List<byte> { 0 }, 1).Copy(); 
        public static BitCode ONE => new BitCode(new List<byte> { 128 }, 1).Copy();

        #endregion

        #region PROPERTIES

        public List<byte> Code { get; private set; }

        public int CodeLength { get; private set; }

        #endregion

        #region BUILDERS

        public BitCode(List<byte> code, int length)
        {
            Code = code;
            CodeLength = length;
        }

        public static BitCode Zeros(uint zerosCount)
        {
            BitCode zeros = new BitCode(new List<byte>(), 0);

            for(uint i = 0; i < zerosCount; i++)
            {
                zeros.Append(ZERO);
            }

            return zeros;
        }

        public static BitCode Ones(uint onesCount)
        {
            BitCode ones = new BitCode(new List<byte>(), 0);

            for (uint i = 0; i < onesCount; i++)
            {
                ones.Append(ONE);
            }

            return ones;
        }

        #endregion

        public BitCode Copy() => new BitCode(Code.ToList(), CodeLength);
        
        public void Append(BitCode newCode)
        {
            if (newCode?.CodeLength > 0)
            {
                if (Code == null || CodeLength == 0)
                {
                    Code = newCode.Code;
                }
                //Aqui viene la magia :-O
                else
                {
                    if (CodeLength % 8 != 0)
                    {
                        //Busco el multiplo de 8, mayor a la longitud del codigo, mas cercano
                        int multip8 = Code.Count * 8;

                        //Calculo la cantidad de bits que siguen al final del codigo (excedente)
                        int excedent = multip8 - CodeLength;
                        int significantBits = 8 - excedent;

                        //Elimino los bits del excedente del codigo actual, poniendolos en 0
                        byte maskSignificant = CommonUtils.MaskLeft(Code.Last(), significantBits);
                        And(Code.Count - 1, maskSignificant);

                        //Obtengo los bits del nuevo codigo que seran desplazados a izquierda,
                        //para ser insertados en el espacio restante del ultimo byte
                        //del codigo completo
                        byte maskExcedent = CommonUtils.MaskLeft(newCode.Code.First(), excedent);
                        maskExcedent >>= significantBits;                       
                        
                        Or(Code.Count - 1, maskExcedent);

                        //Ahora, desplazo los demas bytes del codigo hacia la izquierda 
                        //hasta cubrir los bits que se insertaron en el codigo completo
                        List<byte> shifted = CommonUtils.LeftShifting(newCode.Code, excedent);
                        int newLength = Math.Max(newCode.CodeLength - excedent, 0);

                        //Si al hacer los desplazamientos me quedo un byte de mas, con todos ceros
                        //debo removerlo
                        int newCodeBytesCount = (int)Math.Ceiling((float)newLength / 8.0);

                        if (shifted.Count > newCodeBytesCount)
                        {
                            shifted.RemoveAt(shifted.Count - 1);
                        }

                        newCode.ReplaceCode(shifted, newCode.CodeLength);
                    }

                    //Ultimo, concateno el nuevo codigo con el codigo completo ya modificado
                    Code.AddRange(newCode.Code);
                }

                CodeLength += newCode.CodeLength;
            }
        }

        public void Clean()
        {
            byte mask = CommonUtils.MaskLeft(Code.Last(), CodeLength);
            And(CodeLength - 1, mask);
        }

        public void Insert(uint bitPosition, BitCode encoded)
        {
            BitCode concatenated = GetRange(0, bitPosition);
            concatenated.Append(encoded);
            concatenated.Append(GetRange(bitPosition, (uint)CodeLength - bitPosition));
            ReplaceCode(concatenated.Code, concatenated.CodeLength);
        }

        public BitCode ElementAt(uint bitPosition)
        {
            uint bytePosition = CommonUtils.BitPositionToBytePosition(bitPosition);
            uint effectiveBitPosition = bitPosition % 8;
            return new BitCode(
                new List<byte>() { (byte)((Code[(int)bytePosition] >> (byte)(7 - effectiveBitPosition)) << 7) }, 
                1);
        }

        public List<int> ToIntList()
        {
            List<int> result = new List<int>(CodeLength);

            for(uint i = 0; i < CodeLength; i++)
            {
                BitCode currentBit = ElementAt(i);

                if (currentBit.Equals(ZERO))
                {
                    result.Add(0);
                }
                else
                {
                    result.Add(1);
                }
            }

            return result;
        }

        /// <summary>
        /// Realiza un And entre el byte del Codigo cuyo indice es 'byteIndex' y 'b'
        /// </summary>
        /// <param name="byteIndex">Indice del byte del codigo sobre el que se realizara el And</param>
        /// <param name="b">Byte con el que se realizara el And</param>
        public void And(int byteIndex, byte b)
        {
            if ((Code.Count - 1) >= byteIndex)
            {
                //Reemplazo el byte en la posicion indicada
                //por el resultado de realizar un OR entre
                //dicho byte y el byte 'b'
                Code[byteIndex] &= b;
            }
        }

        /// <summary>
        /// Realiza un Or entre el byte del Codigo cuyo indice es 'byteIndex' y 'b'
        /// </summary>
        /// <param name="byteIndex">Indice del byte del codigo sobre el que se realizara el Or</param>
        /// <param name="b">Byte con el que se realizara el Or</param>
        public void Or(int byteIndex, byte b)
        {
            if((Code.Count - 1) >= byteIndex)
            {
                //Reemplazo el byte en la posicion indicada
                //por el resultado de realizar un OR entre
                //dicho byte y el byte 'b'
                Code[byteIndex] |= b; 
            }
        }

        public void ReplaceCode(List<byte> code, int length)
        {
            Code = code;
            CodeLength = length;
        }

        #region HAMMING

        public BitCode GetRange(uint startBitPos, uint bitsCount)
        {
            int startBytePos = (int)CommonUtils.BitPositionToBytePosition(startBitPos);
            int bytesCount = (int)CommonUtils.BitsLengthToBytesLength(bitsCount);

            //TODO: hacer shifts para dejar solamente marcados los bits de interes y el resto en 0
            return new BitCode(Code.GetRange(startBytePos, bytesCount), (int)bitsCount);
        }

        public List<BitCode> Explode(uint blockBitsSize)
        {
            BitCode copy = Copy();

            uint encodedStrBytesSize = CommonUtils.BitsLengthToBytesLength((uint)CodeLength);
            uint blockBytesSize = CommonUtils.BitsLengthToBytesLength(blockBitsSize);

            //Si los bloques en los que debo explotar al BitCode son mas grandes que el tamaño del BitCode
            if (blockBytesSize > encodedStrBytesSize)
            {
                //Agrego Ceros al BitCode para rellenar
                copy.Append(Zeros(blockBytesSize - encodedStrBytesSize));
            }
            //Si el BitCode es más grande que el tamaño de los bloques en los que debo explotarlo
            else if(encodedStrBytesSize > blockBytesSize)
            {
                uint mod = (uint)copy.CodeLength % blockBitsSize;

                //Si el tamaño del BitCode no es múltiplo del tamaño de bloque
                if (mod != 0)
                {
                    //Agrego tantos ceros cómo sea necesario, 
                    //hasta que el tamaño del BitCode sea múltiplo del tamaño de bloque
                    uint cantZeros = (uint)(Math.Ceiling((float)copy.CodeLength / blockBitsSize) * blockBitsSize - copy.CodeLength);
                    copy.Append(Zeros(cantZeros));
                }
            }

            //Construyo la lista de bloques
            List<BitCode> blocks = new List<BitCode>();

            for (uint i = 0; i < copy.CodeLength; i += blockBitsSize)
            {
                blocks.Add(copy.GetRange(i, blockBitsSize));
            }

            return blocks;
        }

        #endregion

        #region INHERITED

        public override bool Equals(object obj)
        {
            bool result = false;

            if (obj != null && GetType() == obj.GetType())
            {
                BitCode enc = obj as BitCode;

                if (Code.Count == enc.Code.Count && CodeLength == enc.CodeLength)
                {
                    int codeLength = CodeLength;

                    for (int i = 0; i < Code.Count; i++)
                    {
                        if (i == Code.Count - 1)
                        {
                            int diff = 8 - codeLength;
                            result = Code[i] >> diff == enc.Code[i] >> diff;
                        }
                        else if(Code[i] != enc.Code[i])
                        {
                            break;
                        }
                        else
                        {
                            codeLength -= 8;
                        }
                    }
                }
            }

            return result;
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            // TODO: write your implementation of GetHashCode() here
            return base.GetHashCode();
        }

        #endregion
    }
}
