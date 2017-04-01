﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class EncodedString
    {
        #region CONSTS

        public static EncodedString ZERO => new EncodedString(new List<byte> { 0 }, 1).Copy(); 
        public static EncodedString ONE => new EncodedString(new List<byte> { 128 }, 1).Copy();

        #endregion

        public List<byte> Code { get; private set; }

        public int CodeLength { get; private set; }

        public EncodedString(List<byte> code, int length)
        {
            Code = code;
            CodeLength = length;
        }

        public EncodedString Copy() => new EncodedString(Code.ToList(), CodeLength);

        public void Append(EncodedString code)
        {
            if (code != null)
            {
                if (Code == null)
                {
                    Code = code.Code;
                }
                else
                {
                    Code.AddRange(code.Code);
                }
                CodeLength += code.CodeLength;
            }
        }

        public void Append2(EncodedString newCode)
        {
            if (newCode != null)
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
                        //Busco el multiplo de 8, mayor a la longitud el codigo, mas cercano
                        int multip8 = Code.Count * 8;

                        //Calculo la cantidad de bits que siguen al final del codigo (excedente)
                        int excedent = multip8 - CodeLength;
                        int significantBits = 8 - excedent;

                        //Obtengo los bits del nuevo codigo que seran desplazados a izquierda,
                        //para ser insertados en el espacio restante del ultimo byte
                        //del codigo completo
                        byte masked = CommonUtils.MaskLeft(newCode.Code.First(), excedent);
                        masked >>= significantBits;

                        Or(Code.Count - 1, masked);

                        //Ahora, desplazo los demas bytes del codigo hacia la izquierda 
                        //hasta cubrir los bits que se insertaron en el codigo completo
                        List<byte> shifted = CommonUtils.LeftShifting(newCode.Code, excedent);
                        int newLength = newCode.CodeLength - excedent;

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

        public string GetEncodedString() => new UTF8Encoding().GetString(Code.ToArray());            
    }
}
