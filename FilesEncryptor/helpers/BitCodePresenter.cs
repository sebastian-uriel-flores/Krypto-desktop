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
        public static bool ENABLED = true;
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

        public void Print(LinesDisposition disposition, string codeName, int interSpacing = 4)
        {
            if (ENABLED)
            {
                List<string> lines = new List<string>();
                int rowsCount = 0;
                int columnsCount = 0;

                if (disposition == LinesDisposition.Row)
                {
                    if (_codes.Count > 0)
                    {
                        //Dado que cada BitCode corresponde a una fila
                        //la cantidad de filas será la cantidad de BitCodes
                        rowsCount = _codes.Count;

                        //Dado que todas las filas tienen la misma longitud,
                        //la cantidad de columnas será la cantidad de bits de una fila
                        columnsCount = _codes[0].CodeLength;

                        foreach (BitCode code in _codes)
                        {
                            string currentLine = "";
                            List<int> bitsList = code.ToIntList();

                            for (int pos = 0; pos < bitsList.Count; pos++)
                            {
                                currentLine += bitsList[pos].ToString();

                                if ((pos + 1) % interSpacing == 0)
                                {
                                    currentLine += " ";
                                }
                            }

                            lines.Add(currentLine.TrimEnd(' '));
                        }
                    }
                }
                else if (disposition == LinesDisposition.Column)
                {
                    if (_codes.Count > 0)
                    {
                        //Dado que cada BitCode corresponde a una columna
                        //la cantidad de columnas será la cantidad de BitCodes
                        columnsCount = _codes.Count;

                        //Obtengo la cantidad de filas que hay en todas las columnas
                        //Dado que son todas iguales, consultare por la cantidad en la primera columna
                        rowsCount = _codes[0].CodeLength;

                        //Convierto a cada columna en una lista de enteros
                        List<List<int>> bitsColumns = new List<List<int>>();
                        foreach (BitCode column in _codes)
                        {
                            bitsColumns.Add(column.ToIntList());
                        }

                        //Por cada fila
                        for (int rowIndex = 0; rowIndex < rowsCount; rowIndex++)
                        {
                            string currentLine = "";

                            //Por cada columna, agrego a la línea de texto actual el bit en la fila actual
                            for (int columnIndex = 0; columnIndex < bitsColumns.Count; columnIndex++)
                            {
                                currentLine += bitsColumns[columnIndex][rowIndex].ToString();

                                if ((columnIndex + 1) % interSpacing == 0)
                                {
                                    currentLine += " ";
                                }
                            }

                            lines.Add(currentLine.TrimEnd(' '));
                        }
                    }
                }

                Debug.WriteLine(codeName, "[NAME]");
                Debug.WriteLine(string.Format("{0} rows", rowsCount), "[INFO]");
                Debug.WriteLine(string.Format("{0} columns", columnsCount), "[INFO]");
                Debug.WriteLine(string.Join("\n", lines));
                Debug.WriteLine(" ");
            }
        }

        public async void Dump()
        {
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            StorageFile sampleFile = await storageFolder.CreateFileAsync("sample.txt", CreationCollisionOption.ReplaceExisting);
        }
    }
}
