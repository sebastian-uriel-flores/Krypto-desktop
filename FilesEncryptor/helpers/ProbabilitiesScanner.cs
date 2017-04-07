using FilesEncryptor.dto;
using FilesEncryptor.utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers
{
    public class ProbabilitiesScanner
    {
        private Dictionary<char, EncodedString> _codesTable;

        public string EncodedProbabilitiesTable { get; private set; }

        public ReadOnlyDictionary<char, EncodedString> CodesTable => new ReadOnlyDictionary<char, EncodedString>(_codesTable);

        public string Text { get; set; }


        private ProbabilitiesScanner()
        {
            Text = "";
            _codesTable = new Dictionary<char, EncodedString>();
        }

        public EncodedString GetCode(char c) => _codesTable != null && _codesTable.ContainsKey(c) ? _codesTable[c].Copy() : null;

        public bool ContainsChar(EncodedString encoded)
        {
            if(_codesTable != null)
            {
                foreach(EncodedString enc in _codesTable.Values)
                {
                    if(enc.Equals(encoded))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
            
            //=> _codesTable != null && _codesTable.Values.Count(enc => enc.Code.SequenceEqual(encoded.Code)) == 1;

        public char GetChar(EncodedString encoded) => _codesTable.First(pair => pair.Value.Equals(encoded)).Key;

        public bool AreAllDifferent()
        {
            bool result = false;

            foreach(KeyValuePair<char, EncodedString> pair in _codesTable)
            {
                result = !_codesTable.ToList().Exists(pair2 => pair2.Key != pair.Key && pair2.Value.Equals(pair.Value));

                if(!result)
                {
                    break;
                }
            }

            var a =_codesTable['e'];
            var b = _codesTable['s'];

            return result;
        }

        #region FROM_TEXT

        public static async Task<ProbabilitiesScanner> FromText(string text)
        {
            ProbabilitiesScanner scanner = new ProbabilitiesScanner();

            await Task.Factory.StartNew(() =>
            {
                if (text != null)
                {
                    scanner.Text = text;
                    Dictionary<char, float> charsCount = new Dictionary<char, float>();

                    //Primero, obtengo las cantidades de cada caracter del texto
                    foreach (char c in text)
                    {
                        if (charsCount.ContainsKey(c))
                        {
                            charsCount[c]++;
                        }
                        else
                        {
                            charsCount.Add(c, 1);
                        }
                    }

                    //Ahora que tengo las cantidades, calculo las probabilidades
                    foreach (char key in charsCount.Keys.ToList())
                    {
                        charsCount[key] /= text.Length;
                    }

                    //A continuacion, ordeno las probabilidades de mayor a menor
                    var probabilitiesList = charsCount.ToList();
                    probabilitiesList.Sort((a, b) => a.Value < b.Value
                        ? 1
                        : a.Value > b.Value
                            ? -1
                            : 0);

                    scanner._codesTable = ApplyHuffman(probabilitiesList);
                    scanner.EncodedProbabilitiesTable =
                        scanner._codesTable.Select(pair => string.Format("{0}{1}:{2}", pair.Key, pair.Value.CodeLength, pair.Value.GetEncodedString()))
                        .Aggregate((a, b) => a + b);
                }
            });

            bool dif = scanner.AreAllDifferent();

            return scanner;
        }

        /// <summary>
        /// Crea el arbol Huffman
        /// </summary>
        /// <param name="probabilities"></param>
        /// <returns></returns>
        private static Dictionary<char, EncodedString> ApplyHuffman(List<KeyValuePair<char, float>> probabilities)
        {
            //Creo el arbol Huffman
            List<List<HuffmanTreeNode>> huffmanTree = new List<List<HuffmanTreeNode>>();

            //Creo una lista con todas las hojas del arbol Huffman
            var nodes = new List<HuffmanTreeNode>();

            foreach(KeyValuePair<char,float> value in probabilities)
            {
                nodes.Add(new HuffmanTreeNode(value.Value, null));
            }

            huffmanTree.Add(nodes);

            List<HuffmanTreeNode> lastSubTree = null;
            List<HuffmanTreeNode> currentSubTree = nodes.ToList();

            while(currentSubTree.Count > 2)
            {
                //Obtengo los 2 ultimos elemenos de la lista
                var last = currentSubTree[currentSubTree.Count - 1];
                var previousOfLast = currentSubTree[currentSubTree.Count - 2];

                List<HuffmanTreeNode> parents = new List<HuffmanTreeNode>() { previousOfLast, last };
                
                //Sumo sus probabilidades
                var add = new HuffmanTreeNode(last.Probability + previousOfLast.Probability, parents);

                List<HuffmanTreeNode> nextLevel = currentSubTree.ToList();

                //En el siguiente nivel del arbol ya no existen los 2 ultimos elementos del nivel anterior
                nextLevel.RemoveRange(nextLevel.Count - 2, 2);

                //Ahora inserto el nodo compuesto por la suma de probabilidades de los 2 ultimos elementos del nivel anterior
                //arriba de todos sus nodos menores o iguales.
                bool added = false;
                for (int i=0; i < nextLevel.Count; i++)
                {
                    if(add.Probability >= nextLevel[i].Probability)
                    {
                        nextLevel.Insert(i, add);
                        added = true;
                        break;
                    }                    
                }

                //Si no fue insertado, entonces va al final de la lista
                if(!added)
                {
                    nextLevel.Add(add);
                }
                                
                lastSubTree = currentSubTree;
                currentSubTree = nextLevel;

                //Agrego el nuevo nivel al principio de la lista, tal y como si fuera una pila
                huffmanTree.Insert(0, currentSubTree);
            }

            //Una vez que llego a la raiz del arbol, empiezo a generar los codigos            
            SetParentsCodesRecursively(huffmanTree.First().First(), EncodedString.ZERO); //Primer nodo posee un 0
            SetParentsCodesRecursively(huffmanTree.First().Last(), EncodedString.ONE); //Segundo nodo posee un 1

            //Ahora que todas las hojas tienen un código asignado, creo la tabla de códigos
            Dictionary<char, EncodedString> codesTable = new Dictionary<char, EncodedString>();

            for(int node = 0; node < huffmanTree.Last().Count; node++)
            {
                codesTable.Add(probabilities[node].Key, huffmanTree.Last()[node].Code);
            }

            return codesTable;
        }

        private static void SetParentsCodesRecursively(HuffmanTreeNode node, EncodedString code)
        {
            node.Code = code;
            
            if(node.ParentsPositions != null && node.ParentsPositions.Count > 0)
            {
                //Si tiene un solo padre
                if(node.ParentsPositions.Count == 1)
                {
                    SetParentsCodesRecursively(node.ParentsPositions.First(), code);
                }
                //Si tiene 2 padres
                else
                {
                    var firstParentCode = code.Copy();
                    var lastParentCode = code.Copy();

                    //Agrego al final del codigo un 0
                    firstParentCode.Append(EncodedString.ZERO);

                    //Agrego al final del codigo un 1
                    lastParentCode.Append(EncodedString.ONE);

                    SetParentsCodesRecursively(node.ParentsPositions.First(), firstParentCode);
                    SetParentsCodesRecursively(node.ParentsPositions.Last(), lastParentCode);
                }
            }
        }
            
        #endregion

        #region FROM_ENCODED_TABLE

        public static async Task<ProbabilitiesScanner> FromEncodedTable(string encoded, Encoding encoding)
        {
            ProbabilitiesScanner scanner = new ProbabilitiesScanner();

            await Task.Factory.StartNew(() =>
            {
                while (encoded.Count() > 0)
                {
                    //Obtengo el siguiente caracter clave de la tabla
                    char key = encoded[0];

                    //Leo la longitud en bits del codigo asociado al caracter 'key'
                    int index = 1;
                    char pointer = encoded[index];
                    string codeLengthStr = "";

                    while (pointer != ':')
                    {
                        codeLengthStr += pointer;
                        index++;
                        pointer = encoded[index];
                    }

                    //Elimino el caracter y la longitud del codigo, ya leidos previamente
                    encoded = encoded.Remove(0, index + 1);

                    int codeBitsLength = int.Parse(codeLengthStr);
                    int codeBytesLength = (int)CommonUtils.BitsLengthToBytesLength((uint)codeBitsLength);
                    List<byte> codeBytes = new List<byte>();
                                        
                    foreach (byte b in encoded)
                    {
                        if (codeBytes.Count < codeBytesLength)
                        {
                            codeBytes.Add(b);
                        }
                        else
                        {
                            string str = encoding.GetString(codeBytes.ToArray());
                            var bts = encoding.GetBytes(str);

                        if (key == 'e')
                            {

                            }
                            //Agrego el nuevo codigo junto con su clave al diccionario
                            scanner._codesTable.Add(key, new EncodedString(bts.ToList(), codeBitsLength));
                            break;
                        }
                    }

                    //Elimino los bytes correspondientes al codigo
                    encoded = encoded.Remove(0, encoding.GetString(codeBytes.ToArray()).Length);
                }                
            });

            bool dif = scanner.AreAllDifferent();
            return scanner;
        }

        #endregion

        #region FROM_DICTIONARY

        public static ProbabilitiesScanner FromDictionary(Dictionary<char, EncodedString> probabilitiesTable) 
            => new ProbabilitiesScanner() { _codesTable = probabilitiesTable };

        #endregion

        private class HuffmanTreeNode
        {
            public float Probability { get; set; }

            public List<HuffmanTreeNode> ParentsPositions { get; set; }

            public EncodedString Code { get; set; }

            public HuffmanTreeNode(float prob, List<HuffmanTreeNode> parentsPositions)
            {
                Probability = prob;
                ParentsPositions = parentsPositions;                
            }
        }
    }
}
