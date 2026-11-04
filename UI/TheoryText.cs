namespace LaboratorioPestanas.UI
{
    internal static class TheoryText
    {
        public const string Content =
@"MARCO TEÓRICO RESUMIDO — PROGRAMA 1 (encadenamiento separado)
══════════════════════════════════════════════════════════════

Notación asintótica
  f(n) ∈ O(g(n))  ⇔  ∃ c > 0, n₀ ≥ 1 tales que 0 ≤ f(n) ≤ c·g(n) para todo n ≥ n₀.
  Se mide aquí el costo como número de COMPARACIONES DE CLAVE e ITERACIONES del ciclo.

1) Búsqueda secuencial — O(n)
  Recorre A[0..n−1] hasta hallar la clave.
  · Éxito (clave equiprobable): E[C] = (1/n)·Σ i = (n+1)/2  → Θ(n)
  · Fracaso: C = n exactamente                          → Θ(n)
  · No requiere orden ni memoria extra: O(1) espacio.

2) Búsqueda binaria — O(log n)
  Precondición: A ordenado ascendentemente (ordenar cuesta O(n log n) una vez).
  Cada iteración descarta la mitad del intervalo: n, n/2, n/4, …, 1
  ⇒ a lo sumo ⌊log₂ n⌋ + 1 iteraciones.
  · Este programa hace 2 comparaciones por iteración (== y <), salvo la última
    de un éxito (solo ==): C_éxito = 2·I − 1,  C_fracaso = 2·I.
  · Éxito promedio ≈ log₂ n − 1 iteraciones; fracaso ≈ log₂ n iteraciones.

3) Tabla hash con encadenamiento separado — O(1) esperado
  h(k) = k mod m, con m primo ≥ n / 0.75   (método de la división).
  Colisiones: cada cubeta es una lista enlazada; se inserta al inicio.
  Factor de carga α = n / m ≤ 0.75.
  Bajo la hipótesis de dispersión uniforme simple (Cormen et al., Teo. 11.1–11.2):
  · Fracaso: E[C] = α            → Θ(1 + α)
  · Éxito:   E[C] = 1 + α/2 − α/(2n) → Θ(1 + α)
  · Peor caso (todas las claves en una cubeta): Θ(n).
  Como α está acotado por una constante, el costo esperado es O(1).

Definiciones de conteo usadas por el programa
  Comparación = cada prueba entre la clave buscada y un elemento (==, <).
  Iteración   = cada pasada del ciclo principal; en la tabla hash, cada nodo visitado
                (una cubeta vacía cuesta 0 comparaciones y 0 iteraciones, solo 1 cálculo de h).

Diseño experimental
  · N ∈ {1 000, 10 000, 100 000} (opcionalmente 500 000 y 1 000 000).
  · Datos: N enteros distintos uniformes en [0, 10N) generados con SplitMix64.
  · Semilla por tamaño = semilla + N, para que cada N sea reproducible.
  · Consultas: Q claves existentes (posiciones al azar) y Q no existentes del mismo rango.
  · Se reportan promedio, mínimo y máximo de comparaciones e iteraciones,
    además de la referencia teórica y el tiempo medio (informativo, depende del equipo).";
    }
}
