using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace FilesEncryptor.helpers
{
    public class BitCodePresenter
    {
        public enum LinesDisposition
        {
            Row, Column
        }
        private List<BitCode> _codes;

        public BitCodePresenter()
        {
            _codes = new List<BitCode>();
        }

        public static BitCodePresenter From(List<BitCode> codes)
        {
            var codePresenter = new BitCodePresenter();
            codePresenter.Add(codes);

            return codePresenter;
        }
        
        public void Add(List<BitCode> codes)
        {
            _codes.AddRange(codes);
        }

        public void Print(LinesDisposition disposition)
        {
            List<string> lines = new List<string>();

            if (disposition == LinesDisposition.Row)
            {
                foreach (BitCode code in _codes)
                {
                    string currentLine = "";
                    List<int> bitsList = code.ToIntList();

                    for (int pos = 0; pos < bitsList.Count; pos++)
                    {
                        currentLine += bitsList[pos].ToString();

                        if ((pos + 1) % 4 == 0)
                        {
                            currentLine += " ";
                        }
                    }

                    lines.Add(currentLine.TrimEnd(' '));
                }
            }
            else if(disposition == LinesDisposition.Column)
            {
                //Obtengo la cantidad de filas que hay en todas las columnas
                //Dado que son todas iguales, consultare por la cantidad en la primera columna
                int rowsCount = _codes[0].CodeLength;

                //Convierto a cada columna en una lista de enteros
                List<List<int>> bitsColumns = new List<List<int>>();
                foreach(BitCode column in _codes)
                {
                    bitsColumns.Add(column.ToIntList());
                }

                //Por cada fila
                for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
                {
                    string currentLine = "";

                    //Por cada columna, agrego a la línea de texto actual el bit en la fila actual
                    for(int columnIndex = 0; columnIndex < bitsColumns.Count; columnIndex++)
                    {
                        currentLine += bitsColumns[columnIndex][rowIndex].ToString();

                        if((columnIndex + 1) % 4 == 0)
                        {
                            currentLine += " ";
                        }
                    }

                    lines.Add(currentLine.TrimEnd(' '));
                }
            }
            
            Debug.Write(string.Join("\n", lines));
        }

        public void Dump()
        {
            List<string> lines = new List<string>();

            foreach (BitCode code in _codes)
            {
                string currentLine = "";

                foreach (BitCode bit in code.Explode(1))
                {
                    currentLine += bit.Code[0].ToString() + " ";
                }

                lines.Add(currentLine.TrimEnd(' '));
            }

            Debug.Write(string.Join(" ", lines));
        }
    }
}
