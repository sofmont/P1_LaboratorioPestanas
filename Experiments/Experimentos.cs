using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using LaboratorioPestanas.Core;

namespace LaboratorioPestanas.Experiments
{
    /// <summary>Conjunto de N enteros DISTINTOS en [0, 10N) con sus tres estructuras de búsqueda ya construidas.</summary>
    public sealed class DataSet
    {
        public const int MinSize = 10;
        public const int MaxSize = 1_000_000;
        public const double MaxLoadFactor = 0.75;

        private readonly HashSet<int> _members;

        private DataSet(int[] original, HashSet<int> members)
        {
            Original = original;
            _members = members;

            Sorted = (int[])original.Clone();
            Array.Sort(Sorted);
            if (!BinarySearch.IsSortedAscending(Sorted))
                throw new InvalidOperationException("El arreglo no quedó ordenado; la búsqueda binaria no es aplicable.");

            Table = new ChainedHashTable(original.Length, MaxLoadFactor);
            for (int i = 0; i < original.Length; i++)
                if (!Table.Insert(original[i], i))
                    throw new InvalidOperationException("Clave duplicada detectada: " + original[i]);
        }

        public int Size => Original.Length;
        public int UpperBound => Size * 10;
        public int[] Original { get; }
        public int[] Sorted { get; }
        public ChainedHashTable Table { get; }

        public bool Contains(int key) => _members.Contains(key);

        public static void ValidateSize(int n)
        {
            if (n < MinSize || n > MaxSize)
                throw new ArgumentOutOfRangeException(nameof(n),
                    $"N debe estar entre {MinSize:N0} y {MaxSize:N0}.");
        }

        public static DataSet Generate(int n, SplitMix64 rng)
        {
            ValidateSize(n);
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var members = new HashSet<int>();
            var values = new int[n];
            int filled = 0;
            int bound = n * 10;
            while (filled < n)
            {
                int candidate = rng.NextInt(bound);
                if (members.Add(candidate)) values[filled++] = candidate;
            }
            return new DataSet(values, members);
        }

        /// <summary>Claves existentes: posiciones aleatorias (con reemplazo) del conjunto original.</summary>
        public int[] DrawExistingKeys(int count, SplitMix64 rng)
        {
            var keys = new int[count];
            for (int i = 0; i < count; i++) keys[i] = Original[rng.NextInt(Size)];
            return keys;
        }

        /// <summary>Claves inexistentes: valores del mismo rango [0, 10N) que NO pertenecen al conjunto.</summary>
        public int[] DrawMissingKeys(int count, SplitMix64 rng)
        {
            var keys = new int[count];
            int filled = 0;
            while (filled < count)
            {
                int candidate = rng.NextInt(UpperBound);
                if (!_members.Contains(candidate)) keys[filled++] = candidate;
            }
            return keys;
        }
    }

    public sealed class ExperimentConfig
    {
        public ExperimentConfig(IReadOnlyList<int> sizes, int queriesPerCase, ulong seed)
        {
            if (sizes == null || sizes.Count == 0)
                throw new ArgumentException("Seleccione al menos un tamaño N.");
            foreach (int n in sizes) DataSet.ValidateSize(n);
            if (queriesPerCase < 1 || queriesPerCase > 100_000)
                throw new ArgumentOutOfRangeException(nameof(queriesPerCase), "Las consultas por caso deben estar entre 1 y 100 000.");

            Sizes = sizes;
            QueriesPerCase = queriesPerCase;
            Seed = seed;
        }

        public IReadOnlyList<int> Sizes { get; }
        public int QueriesPerCase { get; }
        public ulong Seed { get; }
    }

    public sealed class ExperimentRow
    {
        public int N { get; set; }
        public string Technique { get; set; }
        public string Case { get; set; }
        public double AvgComparisons { get; set; }
        public long MinComparisons { get; set; }
        public long MaxComparisons { get; set; }
        public double AvgIterations { get; set; }
        public long MaxIterations { get; set; }
        public double TheoreticalComparisons { get; set; }
        public double AvgMicroseconds { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>Acumulador de estadísticas (promedio, mínimo, máximo).</summary>
    internal sealed class Accumulator
    {
        private long _count, _sumC, _sumI;
        private long _minC = long.MaxValue, _maxC, _maxI;

        public void Add(SearchResult r, bool expectedFound)
        {
            if (r.Found != expectedFound)
                throw new InvalidOperationException("Inconsistencia: una búsqueda devolvió un resultado incorrecto.");
            _count++;
            _sumC += r.Comparisons;
            _sumI += r.Iterations;
            if (r.Comparisons < _minC) _minC = r.Comparisons;
            if (r.Comparisons > _maxC) _maxC = r.Comparisons;
            if (r.Iterations > _maxI) _maxI = r.Iterations;
        }

        public ExperimentRow ToRow(int n, string technique, string caseName, double theory, double micro, string notes)
        {
            return new ExperimentRow
            {
                N = n,
                Technique = technique,
                Case = caseName,
                AvgComparisons = (double)_sumC / _count,
                MinComparisons = _minC,
                MaxComparisons = _maxC,
                AvgIterations = (double)_sumI / _count,
                MaxIterations = _maxI,
                TheoreticalComparisons = theory,
                AvgMicroseconds = micro,
                Notes = notes
            };
        }
    }

    public static class ExperimentRunner
    {
        public const string Linear = "Secuencial";
        public const string Binary = "Binaria";
        public const string Hash = "Tabla hash (encadenamiento)";
        public const string Hit = "Existente";
        public const string Miss = "No existente";

        public static List<ExperimentRow> Run(ExperimentConfig cfg, IProgress<string> progress, CancellationToken token)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var rows = new List<ExperimentRow>();

            foreach (int n in cfg.Sizes)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report($"Generando N = {n:N0} …");

                // Una semilla derivada por tamaño: cada N es reproducible de forma independiente.
                var rng = new SplitMix64(unchecked(cfg.Seed + (ulong)n));
                DataSet ds = DataSet.Generate(n, rng);
                int[] hits = ds.DrawExistingKeys(cfg.QueriesPerCase, rng);
                int[] misses = ds.DrawMissingKeys(cfg.QueriesPerCase, rng);

                double alpha = ds.Table.LoadFactor;
                double log2n = Math.Log(n, 2);
                string hashNote = string.Format(CultureInfo.InvariantCulture,
                    "m={0}, α={1:F3}, cadena máx={2}", ds.Table.Capacity, alpha, ds.Table.LongestChain());

                progress?.Report($"N = {n:N0}: búsqueda secuencial …");
                rows.Add(Measure(n, Linear, Hit, hits, true, k => SequentialSearch.Find(ds.Original, k), (n + 1) / 2.0, "", token));
                rows.Add(Measure(n, Linear, Miss, misses, false, k => SequentialSearch.Find(ds.Original, k), n, "", token));

                progress?.Report($"N = {n:N0}: búsqueda binaria …");
                rows.Add(Measure(n, Binary, Hit, hits, true, k => BinarySearch.Find(ds.Sorted, k), 2 * log2n - 3, "", token));
                rows.Add(Measure(n, Binary, Miss, misses, false, k => BinarySearch.Find(ds.Sorted, k), 2 * (Math.Floor(log2n) + 1), "", token));

                progress?.Report($"N = {n:N0}: tabla hash …");
                rows.Add(Measure(n, Hash, Hit, hits, true, k => ds.Table.Find(k), 1 + alpha / 2, hashNote, token));
                rows.Add(Measure(n, Hash, Miss, misses, false, k => ds.Table.Find(k), alpha, hashNote, token));
            }
            progress?.Report("Experimento terminado.");
            return rows;
        }

        private static ExperimentRow Measure(int n, string technique, string caseName, int[] keys, bool expected,
            Func<int, SearchResult> search, double theory, string notes, CancellationToken token)
        {
            var acc = new Accumulator();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < keys.Length; i++)
            {
                if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                acc.Add(search(keys[i]), expected);
            }
            sw.Stop();
            double micro = sw.Elapsed.TotalMilliseconds * 1000.0 / keys.Length;
            return acc.ToRow(n, technique, caseName, theory, micro, notes);
        }

        public static void ExportCsv(IEnumerable<ExperimentRow> rows, string path)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruta de archivo inválida.", nameof(path));

            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("N,Tecnica,Caso,CompProm,CompMin,CompMax,IterProm,IterMax,CompTeorica,MicrosegProm,Notas");
            foreach (ExperimentRow r in rows)
            {
                sb.Append(r.N.ToString(inv)).Append(',')
                  .Append(Quote(r.Technique)).Append(',')
                  .Append(Quote(r.Case)).Append(',')
                  .Append(r.AvgComparisons.ToString("F4", inv)).Append(',')
                  .Append(r.MinComparisons.ToString(inv)).Append(',')
                  .Append(r.MaxComparisons.ToString(inv)).Append(',')
                  .Append(r.AvgIterations.ToString("F4", inv)).Append(',')
                  .Append(r.MaxIterations.ToString(inv)).Append(',')
                  .Append(r.TheoreticalComparisons.ToString("F4", inv)).Append(',')
                  .Append(r.AvgMicroseconds.ToString("F4", inv)).Append(',')
                  .AppendLine(Quote(r.Notes));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Quote(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
