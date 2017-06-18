using FilesEncryptor.dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.helpers.huffman
{
    public class BinaryTree<T>
    {
        public static BinaryTree<T> EMPTY => new BinaryTree<T>() { _value = default(T), _leftSon = null,  _rightSon = null };
        private T _value;
        private BinaryTree<T> _leftSon;
        private BinaryTree<T> _rightSon;

        public T Value => _value;

        public BinaryTree<T> LeftSon => _leftSon;

        public BinaryTree<T> RightSon => _rightSon;

        public BinaryTree()
        {
            _value = default(T);
            _leftSon = EMPTY;
            _rightSon = EMPTY;
        }

        public bool Contains(BitCode position)
        {
            List<BitCode> bits = position.Explode(1, false).Item1;

            BinaryTree<T> lastSon = this;
            
            foreach (BitCode bit in bits)
            {
                if (lastSon == null)
                    break;

                if(bit == BitCode.ZERO)
                {
                    lastSon = lastSon.LeftSon;
                }
                else if(bit == BitCode.ONE)
                {
                    lastSon = lastSon.RightSon;
                }
                else
                {
                    lastSon = null;
                }
            }

            return lastSon != null && !lastSon.Value.Equals(default(T));
        }

        public T Get(BitCode position)
        {
            T value = default(T);

            List<BitCode> bits = position.Explode(1, false).Item1;

            BinaryTree<T> lastSon = this;

            foreach (BitCode bit in bits)
            {
                if (lastSon == null)
                    break;

                if (bit == BitCode.ZERO)
                {
                    lastSon = lastSon.LeftSon;
                }
                else if (bit == BitCode.ONE)
                {
                    lastSon = lastSon.RightSon;
                }
                else
                {
                    lastSon = null;
                }
            }

            if(lastSon != null)
            {
                value = lastSon.Value;
            }

            return value;
        }

        public void Add(BitCode position, T value)
        {
            List<BitCode> bits = position.Explode(1, false).Item1;

            BinaryTree<T> lastSon = this;

            foreach (BitCode bit in bits)
            {
                BinaryTree<T> newSon = EMPTY;

                if (bit == BitCode.ZERO)
                {
                    if (lastSon.LeftSon == null)
                    {
                        lastSon._leftSon = new BinaryTree<T>() { _leftSon = EMPTY };                        
                    }

                    newSon = lastSon.LeftSon;
                }
                else if (bit == BitCode.ONE)
                {
                    if (lastSon.RightSon == null)
                    {
                        lastSon._rightSon = new BinaryTree<T>() { _rightSon = EMPTY };
                    }
                    newSon = lastSon.RightSon;
                }

                lastSon = newSon;
            }

            lastSon._value = value;
        }

        public void Remove(BitCode position)
        {
        }
    }
}
