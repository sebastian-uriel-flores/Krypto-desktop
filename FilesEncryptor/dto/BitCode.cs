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

            byte disp1 = (byte)(Code[(int)bytePosition] >> (byte)(7 - effectiveBitPosition));
            byte disp2 = (byte)(disp1 << 7);
            return new BitCode(
                new List<byte>() { disp2 }, 1);
        }

        public BitCode ReplaceAt(uint bitPosition, BitCode replacement)
        {
            BitCode firstHalf = GetRange(0, bitPosition);

            BitCode secondHalf = bitPosition + 1 >= CodeLength
                ? EMPTY 
                : GetRange(bitPosition + 1, (uint)CodeLength - bitPosition - 1);

            firstHalf.Append(replacement);
            firstHalf.Append(secondHalf);
            return firstHalf;
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

        public BitCode Negate()
        {
            BitCode result = Copy();
                        
            //Opero bit a bit
            for (int pos = 0; pos < Code.Count; pos++)
            {
                result.Code[pos] ^= 255;
            }
            
            return result;
        }

        public void ReplaceCode(List<byte> code, int length)
        {
            Code = code;
            CodeLength = length;
        }

        #region HAMMING

        public BitCode GetRange(uint startBitPos, uint bitsCount)
        {
            if(bitsCount == 0)
            {
                return EMPTY;
            }

            int startBytePos = (int)CommonUtils.BitPositionToBytePosition(startBitPos);
            int endBytePos = (int)CommonUtils.BitPositionToBytePosition(startBitPos + bitsCount - 1);
            int bytesCount = (endBytePos - startBytePos) + 1;

            List<byte> bytesRange = Code.GetRange(startBytePos, bytesCount);

            //Hago shifts a la izquierda para eliminar bits que no estan incluidos en el rango
            if (bytesRange.Count > 0)
            {
                bytesRange = CommonUtils.LeftShifting(bytesRange, (int)startBitPos % 8);

                //Pongo en cero los bits a la derecha del final del codigo
                bytesRange[bytesRange.Count - 1] = CommonUtils.MaskLeft(bytesRange.Last(), (int)bitsCount - ((bytesRange.Count - 1) * 8));
            }

            return new BitCode(bytesRange, (int)bitsCount);
        }

        public Tuple<List<BitCode>,int> Explode(uint blockBitsSize, bool fillRemainingWithZeros = true)
        {
            BitCode copy = Copy();
            int addedZeros = 0;
            
            if (fillRemainingWithZeros)
            {
                //Si los bloques en los que debo explotar al BitCode son mas grandes que el tamaño del BitCode
                if (blockBitsSize > (uint)copy.CodeLength)
                {
                    addedZeros = (int)blockBitsSize - copy.CodeLength;

                    //Agrego Ceros al BitCode para rellenar
                    copy.Append(Zeros((uint)addedZeros));
                }
                //Si el BitCode es más grande que el tamaño de los bloques en los que debo explotarlo
                else if ((uint)copy.CodeLength > blockBitsSize)
                {
                    uint mod = (uint)copy.CodeLength % blockBitsSize;

                    //Si el tamaño del BitCode no es múltiplo del tamaño de bloque
                    if (mod != 0)
                    {
                        //Agrego tantos ceros cómo sea necesario, 
                        //hasta que el tamaño del BitCode sea múltiplo del tamaño de bloque
                        addedZeros = (int)(Math.Ceiling((float)copy.CodeLength / blockBitsSize) * blockBitsSize - copy.CodeLength);
                        copy.Append(Zeros((uint)addedZeros));
                    }
                }
            }

            //Construyo la lista de bloques
            List<BitCode> blocks = new List<BitCode>();

            for (uint i = 0; i < copy.CodeLength; i += blockBitsSize)
            {
                if(i <= 2448 && i >= 2432)
                {

                }

                uint bitsToObtain = !fillRemainingWithZeros && i + blockBitsSize >= copy.CodeLength
                    ? (uint)copy.CodeLength - i
                    : blockBitsSize;

                blocks.Add(copy.GetRange(i, bitsToObtain));
            }

            return new Tuple<List<BitCode>, int>(blocks, addedZeros);
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
