using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor
{
    public class HuffmanEncoder
    {
        private ProbabilitiesScanner _probScanner;
        
        public async Task<HuffmanEncodeResult> Encode(string text)
        {
            _probScanner = new ProbabilitiesScanner(text);
            await _probScanner.ScanProbabilities();
            
            EncodedString fullCode = null;
            if(text != null)
            {
                int counter = 0;
                foreach(char c in text)
                {
                    counter++;
                    try
                    {
                        //Obtengo el codigo Huffman para el caracter
                        EncodedString code = _probScanner.GetCode(c);

                        if (fullCode == null)
                        {
                            fullCode = code;
                        }
                        else
                        {
                            fullCode.Append2(code);                         
                        }
                    }
                    catch(Exception ex)
                    {

                    }
                }
            }

            return new HuffmanEncodeResult(fullCode, _probScanner.EncodedProbabilitiesTable);
        }        
    }
}
