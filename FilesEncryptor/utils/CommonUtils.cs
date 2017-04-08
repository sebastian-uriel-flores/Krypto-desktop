using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.utils
{
    public static class CommonUtils
    {
        /// <summary>
        /// Hace desplazamientos a la izquierda entre arreglos de bytes.
        /// </summary>
        /// <param name="bytes">Arreglo de bytes a desplazar</param>
        /// <param name="shifts">Cantidad de desplazamientos, entre 0 y 8</param>
        /// <returns></returns>
        public static List<byte> LeftShifting(List<byte> bytes, int shifts)
        {
            List<byte> copy = bytes.ToList();

            if (copy != null && copy.Count * 8 >= shifts)
            {
                if (shifts >= 8)
                {
                    copy.RemoveRange(0, shifts / 8);
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

        /// <summary>
        /// Hace desplazamientos a la derecha entre arreglos de bytes.
        /// </summary>
        /// <param name="bytes">Arreglo de bytes a desplazar</param>
        /// <param name="shifts">Cantidad de desplazamientos, entre 0 y 8</param>
        /// <returns></returns>
        public static List<byte> RightShifting(List<byte> bytes, int shifts)
        {
            List<byte> copy = bytes.ToList();

            if (copy != null)
            {
                shifts = shifts % 9;
                for (int i = 0; i < bytes.Count; i++)
                {
                    if (i == 0)
                    {
                        //Si es el primer byte, simplemente hago los desplazamientos
                        copy[i] >>= shifts;
                    }
                    else
                    {
                        //Si no es el primer byte

                        //Guardo los bits de mas a la izquierda que seran desplazados,
                        //haciendo uso de la mascara
                        byte masked = MaskRight(copy[i], shifts);

                        //Hago los desplazamientos a izquierda
                        copy[i] >>= shifts;

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

        public static uint BitsLengthToBytesLength(uint bitsLength) => (uint)Math.Ceiling((float)bitsLength / 8.0);
        //(8 - (bitsLength % 9) + bitsLength) / 8;
    }
}
