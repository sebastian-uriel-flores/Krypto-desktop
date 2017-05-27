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
            BitCode zeros = EMPTY;

            for(uint i = 0; i < zerosCount; i++)
            {
                zeros.Append(ZERO);
            }

            return zeros;
        }

        public static BitCode Ones(uint onesCount)
        {
            BitCode ones = EMPTY;

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
                        byte maskSignificant = MaskLeft(Code.Last(), significantBits);
                        And(Code.Count - 1, maskSignificant);

                        //Obtengo los bits del nuevo codigo que seran desplazados a izquierda,
                        //para ser insertados en el espacio restante del ultimo byte
                        //del codigo completo
                        byte maskExcedent = MaskLeft(newCode.Code.First(), excedent);
                        maskExcedent >>= significantBits;                       
                        
                        Or(Code.Count - 1, maskExcedent);

                        //Ahora, desplazo los demas bytes del codigo hacia la izquierda 
                        //hasta cubrir los bits que se insertaron en el codigo completo
                        List<byte> shifted = LeftShifting(newCode.Code, excedent);
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
            int bytePosition = (int)BitPositionToBytePosition(bitPosition);
            int localBitPosition = (int)GlobalBitPositionToLocal(bitPosition);

            //Tomo el byte en el que se encuentra el bit indicado
            byte container = Code[bytePosition];

            //Pongo en cero los bits a la derecha del bit indicado
            container = MaskLeft(container, localBitPosition + 1);

            //Elimino los bits a la izquierda que esten de mas. 
            //Por ejemplo, si se indica el bit 2, tendre 2 bits redundantes a la izquierda.
            //Para eliminarlos hago shifts a la izquierda.
            container <<= localBitPosition;

            return container == 0
                ? ZERO
                : ONE;            
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

        public BitCode GetRange(uint startBitPos, uint bitsCount)
        {
            BitCode result = EMPTY;

            if (bitsCount > 0 && Code.Count > 0)
            {
                //NOTA: Es limite izquierdo inclusivo y limite derecho exclusivo

                uint localStartBitPos = GlobalBitPositionToLocal(startBitPos);
                uint bytesLength = BitsLengthToBytesLength(bitsCount + localStartBitPos);
                uint startBytePos = BitPositionToBytePosition(startBitPos);

                List<byte> bytesRange = new List<byte>();
                try
                {
                    bytesRange = Code.GetRange((int)startBytePos, (int)bytesLength);
                }
                catch(Exception ex)
                {

                }

                if (bytesRange.Count > 0)
                {
                    
                    //Calculo la cantidad de bits usados en el ultimo byte
                    int leftBits = (int)GlobalBitPositionToLocal(startBitPos + bitsCount);

                    //Si da 0, es porque usamos los 8 bit
                    //Sino, pongo en cero los bits a la derecha del final del codigo
                    if (leftBits > 0)
                    {
                        bytesRange[bytesRange.Count - 1] = MaskLeft(bytesRange.Last(), (int)GlobalBitPositionToLocal(startBitPos + bitsCount));
                    }

                    //Elimino los bits a la izquierda que esten de mas. 
                    //Por ejemplo, si empiezo en el bit 2, tendre 2 bits redundantes a la izquierda.
                    //Para eliminarlos hago shifts a la izquierda.
                    bytesRange = LeftShifting(bytesRange, (int)localStartBitPos);
                }

                result = new BitCode(bytesRange, (int)bitsCount);
            }

            return result;
        }

        public Tuple<List<BitCode>, int> Explode(uint blockBitsSize, bool fillRemainingWithZeros = true)
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
                //Si la siguiente palabra es mas chica, es decir,
                //quedan menos bits que 'blockBitsSize', 
                //entonces solo devuelvo los bits restantes
                uint bitsToObtain = i + blockBitsSize >= copy.CodeLength
                    ? (uint)copy.CodeLength - i
                    : blockBitsSize;

                blocks.Add(copy.GetRange(i, bitsToObtain));
            }

            return new Tuple<List<BitCode>, int>(blocks, addedZeros);
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

        #region UTILS

        public static uint BitsLengthToBytesLength(uint bitsLength) => (uint)Math.Ceiling((float)bitsLength / 8.0);

        public static uint BitPositionToBytePosition(uint bitsLength) => (uint)Math.Floor((float)bitsLength / 8.0);

        public static uint GlobalBitPositionToLocal(uint bitPosition) => bitPosition % 8;

        /// <summary>
        /// Hace desplazamientos a la izquierda entre arreglos de bytes.
        /// </summary>
        /// <param name="bytes">Arreglo de bytes a desplazar</param>
        /// <param name="shifts">Cantidad de desplazamientos</param>
        /// <returns></returns>
        public static List<byte> LeftShifting(List<byte> bytes, int shifts)
        {
            List<byte> copy = bytes.ToList();

            if (copy != null && copy.Count * 8 >= shifts)
            {
                //Si se requiere desplazar 1 byte o mas, 
                //entonces elimino todos los bytes posibles de la lista
                //Y luego, solamente quedaran hacer los ultimos n desplazamientos, 
                //con n menor a 1 byte
                if (shifts >= 8)
                {
                    copy.RemoveRange(0, (int)Math.Floor(shifts / 8.0));
                    shifts %= 8;
                }
                
                for (int i = 0; i < copy.Count; i++)
                {
                    if (i == 0)
                    {
                        //Si es el primer byte, simplemente hago los desplazamientos
                        copy[i] <<= shifts;
                    }
                    else
                    {
                        //Si no es el primer byte

                        //Guardo los bits de mas a la izquierda que seran desplazados,
                        //haciendo uso de la mascara
                        byte masked = MaskLeft(copy[i], shifts);

                        //Hago los desplazamientos a izquierda
                        copy[i] <<= shifts;

                        //Corro los bit almacenados, desde el extremo izquierdo
                        //hacia el extremo derecho del byte
                        masked >>= (8 - shifts);

                        //Al byte anterior (el cual ya fue desplazado previamente)
                        //le agrego los bits guardados del byte actual, 
                        //en su extremo derecho
                        copy[i - 1] |= masked;
                    }
                }
            }

            return copy;
        }

        public static byte MaskLeft(byte b, int leftBitsCount)
        {
            byte mask = 255; //Mask = 1111 1111

            //Solamente dejo en la mascara los unos correspondientes
            //a los caracteres de mas a la izquierda que se perderan
            //al hacer los desplazamientos
            mask <<= (8 - leftBitsCount);

            //Guardo los bits de mas a la izquierda que seran desplazados,
            //haciendo uso de la mascara
            byte masked = (byte)(b & mask);

            return masked;
        }

        public static byte MaskRight(byte b, int rightBitsCount)
        {
            byte mask = 255; //Mask = 1111 1111

            //Solamente dejo en la mascara los unos correspondientes
            //a los caracteres de mas a la izquierda que se perderan
            //al hacer los desplazamientos
            mask >>= (8 - rightBitsCount);

            //Guardo los bits de mas a la izquierda que seran desplazados,
            //haciendo uso de la mascara
            byte masked = (byte)(b & mask);

            return masked;
        }

        #endregion
    }
}
