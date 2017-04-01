using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FilesEncryptor
{
    public static class HuffmanCompressor
    {   
        public async static Task<HuffmanEncodeResult> Uncompress(StorageFile compressedFile)
        {
            HuffmanEncodeResult uncompressed = null;

            if(compressedFile != null)
            {                
                try
                {
                    var stream = await compressedFile.OpenAsync(FileAccessMode.Read);
                    ulong size = stream.Size;

                    string probTableString = "";
                    byte[] encodStringBytes;

                    using (var inputStream = stream.GetInputStreamAt(0))
                    {
                        using (var dataReader = new DataReader(inputStream))
                        {
                            uint numBytesLoaded = await dataReader.LoadAsync((uint)size);
                            Encoding u8 = Encoding.UTF8;

                            string probTableLength = "";
                            string temp = dataReader.ReadString(1);
                            while(temp != ".")
                            {
                                probTableLength += temp;
                                temp = dataReader.ReadString(1);
                            }
                            
                            byte[] probTableBytes = new byte[uint.Parse(probTableLength)];
                            dataReader.ReadBytes(probTableBytes);
                            
                            probTableString = u8.GetString(probTableBytes);

                            string codeLength = "";
                            temp = dataReader.ReadString(1);

                            while (temp != ".")
                            {
                                codeLength += temp;
                                temp = dataReader.ReadString(1);
                            }

                            uint codeBytesLength = (uint)Math.Ceiling(float.Parse(codeLength) / 8.0);
                            encodStringBytes = new byte[codeBytesLength];
                            dataReader.ReadBytes(encodStringBytes);
                        }
                    }

                    stream.Dispose();

                    //Ahora, armo la tablas de probabilidades
                    Dictionary<char, EncodedString> table = new Dictionary<char, EncodedString>();
                    char key;
                    bool hasKey = false;
                    int cLength = -1;
                    List<byte> code;
                    
                    foreach (char c in probTableString)
                    {
                        if(!hasKey)
                        {
                            key = c;
                            hasKey = true;
                        }
                        else
                        {
                            cLength=
                        }
                    }


                    /*TODO: Reestructurar ProbabilitiesScanner para que pueda recibir un
                     * un texto comun y calcular las probabilidades,
                     * o bien, recibir una tabla de probabilidades codificada 
                     * y luego, dado un codigo, devolver el caracter correspondiente
                     */
                }
                catch (Exception)
                {

                }
            }

            return uncompressed;
        }
    }
}
