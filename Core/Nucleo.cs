using System;

namespace LaboratorioPestanas.Core
{
    /// <summary>
    /// Generador pseudoaleatorio SplitMix64 (Steele, Lea y Flood, 2014).
    /// Se implementa a mano para que los experimentos sean 100 % reproducibles
    /// en cualquier versión de .NET (System.Random cambia de algoritmo entre versiones).
    /// </summary>
    public sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed) { _state = seed; }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Entero uniforme en [0, exclusiveMax).</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax), "El límite debe ser positivo.");
            return (int)(NextUInt64() % (ulong)exclusiveMax);
        }
    }

    /// <summary>
    /// Resultado inmutable de una búsqueda instrumentada.
    /// Comparaciones = pruebas de clave (==, &lt;); Iteraciones = pasadas del ciclo principal
    /// (en la tabla hash: nodos de la cadena visitados).
    /// </summary>
    public readonly struct SearchResult
    {
        public SearchResult(bool found, int position, long comparisons, long iterations)
        {
            Found = found;
            Position = position;
            Comparisons = comparisons;
            Iterations = iterations;
        }

        public bool Found { get; }
        public int Position { get; }
        public long Comparisons { get; }
        public long Iterations { get; }
    }

    /// <summary>Búsqueda secuencial (lineal). O(n) en el peor caso.</summary>
    public static class SequentialSearch
    {
        public static SearchResult Find(int[] data, int key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            long comparisons = 0, iterations = 0;
            for (int i = 0; i < data.Length; i++)
            {
                iterations++;
                comparisons++;
                if (data[i] == key)
                    return new SearchResult(true, i, comparisons, iterations);
            }
            return new SearchResult(false, -1, comparisons, iterations);
        }
    }

    /// <summary>Búsqueda binaria iterativa sobre un arreglo ordenado ascendentemente. O(log n).</summary>
    public static class BinarySearch
    {
        public static SearchResult Find(int[] sorted, int key)
        {
            if (sorted == null) throw new ArgumentNullException(nameof(sorted));

            long comparisons = 0, iterations = 0;
            int low = 0, high = sorted.Length - 1;
            while (low <= high)
            {
                iterations++;
                int mid = low + ((high - low) >> 1);   // evita el desbordamiento de (low + high) / 2

                comparisons++;
                if (sorted[mid] == key)
                    return new SearchResult(true, mid, comparisons, iterations);

                comparisons++;
                if (sorted[mid] < key) low = mid + 1;
                else high = mid - 1;
            }
            return new SearchResult(false, -1, comparisons, iterations);
        }

        /// <summary>Verifica la precondición de la búsqueda binaria en O(n).</summary>
        public static bool IsSortedAscending(int[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            for (int i = 1; i < data.Length; i++)
                if (data[i - 1] > data[i]) return false;
            return true;
        }
    }

    public static class Primes
    {
        public static bool IsPrime(int n)
        {
            if (n < 2) return false;
            if (n % 2 == 0) return n == 2;
            for (int d = 3; (long)d * d <= n; d += 2)
                if (n % d == 0) return false;
            return true;
        }

        public static int NextPrime(int n)
        {
            if (n <= 2) return 2;
            int candidate = (n % 2 == 0) ? n + 1 : n;
            while (!IsPrime(candidate))
            {
                if (candidate > int.MaxValue - 2)
                    throw new OverflowException("No existe un primo representable mayor o igual a " + n + ".");
                candidate += 2;
            }
            return candidate;
        }
    }

    /// <summary>
    /// Tabla hash implementada desde cero con ENCADENAMIENTO SEPARADO (listas enlazadas propias).
    /// Función de dispersión: método de la división h(k) = k mod m, con m primo.
    /// La capacidad se elige como el primo ≥ n / α_max para acotar el factor de carga α.
    /// </summary>
    public sealed class ChainedHashTable
    {
        private sealed class Node
        {
            public Node(int key, int value, Node next)
            {
                Key = key;
                Value = value;
                Next = next;
            }

            public int Key { get; }
            public int Value { get; }
            public Node Next { get; }
        }

        private readonly Node[] _buckets;

        public ChainedHashTable(int expectedItems, double maxLoadFactor)
        {
            if (expectedItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedItems), "Debe esperarse al menos un elemento.");
            if (double.IsNaN(maxLoadFactor) || maxLoadFactor <= 0 || maxLoadFactor > 10)
                throw new ArgumentOutOfRangeException(nameof(maxLoadFactor), "El factor de carga debe estar en (0, 10].");

            double raw = Math.Ceiling(expectedItems / maxLoadFactor);
            if (raw > int.MaxValue / 2)
                throw new OverflowException("La capacidad requerida es demasiado grande.");
            _buckets = new Node[Primes.NextPrime((int)raw)];
        }

        public int Count { get; private set; }
        public int Capacity => _buckets.Length;
        public double LoadFactor => (double)Count / _buckets.Length;

        /// <summary>Método de la división; se corrige el residuo para claves negativas.</summary>
        public int Hash(int key)
        {
            int r = key % _buckets.Length;
            return r < 0 ? r + _buckets.Length : r;
        }

        /// <summary>Inserta al inicio de la cadena (O(1) tras verificar duplicados). Devuelve false si la clave ya existe.</summary>
        public bool Insert(int key, int value)
        {
            int b = Hash(key);
            for (Node n = _buckets[b]; n != null; n = n.Next)
                if (n.Key == key) return false;

            _buckets[b] = new Node(key, value, _buckets[b]);
            Count++;
            return true;
        }

        public SearchResult Find(int key)
        {
            long comparisons = 0, iterations = 0;
            for (Node n = _buckets[Hash(key)]; n != null; n = n.Next)
            {
                iterations++;
                comparisons++;
                if (n.Key == key)
                    return new SearchResult(true, n.Value, comparisons, iterations);
            }
            return new SearchResult(false, -1, comparisons, iterations);
        }

        public int LongestChain()
        {
            int best = 0;
            foreach (Node head in _buckets)
            {
                int len = 0;
                for (Node n = head; n != null; n = n.Next) len++;
                if (len > best) best = len;
            }
            return best;
        }

        public int UsedBuckets()
        {
            int used = 0;
            foreach (Node head in _buckets) if (head != null) used++;
            return used;
        }
    }
}
