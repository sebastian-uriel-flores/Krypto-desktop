using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class BaseHuffmanCodifier
    {
        protected Dictionary<char, BitCode> _charsCodes;

        public BaseHuffmanCodifier()
        {
            _charsCodes = new Dictionary<char, BitCode>();
        }

        public char GetChar(BitCode encoded) => _charsCodes.First(pair => pair.Value.Equals(encoded)).Key;

        protected BitCode GetCode(char c) => ContainsChar(c) ? _charsCodes[c].Copy() : null;

        protected bool ContainsChar(char c) => _charsCodes != null && _charsCodes.ContainsKey(c);

        protected bool ContainsCode(BitCode encoded) => _charsCodes != null && _charsCodes.ContainsValue(encoded);

        /// <summary>
        /// Crea el arbol Huffman
        /// </summary>
        /// <param name="probabilities"></param>
        /// <returns></returns>
        protected Dictionary<char, BitCode> ApplyHuffman(List<KeyValuePair<char, float>> probabilities)
        {
            //Creo el arbol Huffman
            List<List<HuffmanTreeNode>> huffmanTree = new List<List<HuffmanTreeNode>>();

            //Creo una lista con todas las hojas del arbol Huffman
            var nodes = new List<HuffmanTreeNode>();

            foreach (KeyValuePair<char, float> value in probabilities)
            {
                nodes.Add(new HuffmanTreeNode(value.Value, null));
            }

            huffmanTree.Add(nodes);

            List<HuffmanTreeNode> lastSubTree = null;
            List<HuffmanTreeNode> currentSubTree = nodes.ToList();

            while (currentSubTree.Count > 2)
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
                for (int i = 0; i < nextLevel.Count; i++)
                {
                    if (add.Probability >= nextLevel[i].Probability)
                    {
                        nextLevel.Insert(i, add);
                        added = true;
                        break;
                    }
                }

                //Si no fue insertado, entonces va al final de la lista
                if (!added)
                {
                    nextLevel.Add(add);
                }

                lastSubTree = currentSubTree;
                currentSubTree = nextLevel;

                //Agrego el nuevo nivel al principio de la lista, tal y como si fuera una pila
                huffmanTree.Insert(0, currentSubTree);
            }

            //Una vez que llego a la raiz del arbol, empiezo a generar los codigos            
            SetParentsCodesRecursively(huffmanTree.First().First(), BitCode.ZERO); //Primer nodo posee un 0
            SetParentsCodesRecursively(huffmanTree.First().Last(), BitCode.ONE); //Segundo nodo posee un 1

            //Ahora que todas las hojas tienen un código asignado, creo la tabla de códigos
            Dictionary<char, BitCode> codesTable = new Dictionary<char, BitCode>();

            for (int node = 0; node < huffmanTree.Last().Count; node++)
            {
                codesTable.Add(probabilities[node].Key, huffmanTree.Last()[node].Code);
            }

            return codesTable;
        }

        protected void SetParentsCodesRecursively(HuffmanTreeNode node, BitCode code)
        {
            node.Code = code;

            if (node.ParentsPositions != null && node.ParentsPositions.Count > 0)
            {
                //Si tiene un solo padre
                if (node.ParentsPositions.Count == 1)
                {
                    SetParentsCodesRecursively(node.ParentsPositions.First(), code);
                }
                //Si tiene 2 padres
                else
                {
                    var firstParentCode = code.Copy();
                    var lastParentCode = code.Copy();

                    //Agrego al final del codigo un 0
                    firstParentCode.Append(BitCode.ZERO);

                    //Agrego al final del codigo un 1
                    lastParentCode.Append(BitCode.ONE);

                    SetParentsCodesRecursively(node.ParentsPositions.First(), firstParentCode);
                    SetParentsCodesRecursively(node.ParentsPositions.Last(), lastParentCode);
                }
            }
        }
    }
}
