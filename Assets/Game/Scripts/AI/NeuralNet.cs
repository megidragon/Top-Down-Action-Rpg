using System;
using System.Text;
using UnityEngine;

namespace TinyRpg.AI
{
    /// Perceptron multicapa diminuto, en C# puro y sin dependencias. Pensado
    /// para neuroevolucion: no hay gradientes ni backprop, solo evaluar rapido
    /// y mutar pesos.
    ///
    /// La red es deliberadamente pequena (unos 250 pesos): evaluarla cuesta
    /// menos que una consulta de fisica, asi que se pueden correr cientos de
    /// duelistas a la vez sin que la CPU se note.
    [Serializable]
    public class NeuralNet
    {
        /// Neuronas por capa, entrada incluida. Ej: [14, 12, 6].
        public int[] layers;
        /// Pesos aplanados, con el sesgo al final de cada neurona.
        public float[] weights;

        // Memoria de trabajo reutilizada entre evaluaciones (cero basura).
        [NonSerialized] float[][] activations;

        public int InputCount => layers != null && layers.Length > 0 ? layers[0] : 0;
        public int OutputCount => layers != null && layers.Length > 0 ? layers[layers.Length - 1] : 0;

        public static int WeightCountFor(int[] layers)
        {
            int total = 0;
            for (int i = 1; i < layers.Length; i++)
                total += (layers[i - 1] + 1) * layers[i]; // +1 = sesgo
            return total;
        }

        public static NeuralNet CreateRandom(int[] layers, System.Random rng, float range = 1.2f)
        {
            var net = new NeuralNet { layers = (int[])layers.Clone() };
            net.weights = new float[WeightCountFor(layers)];
            for (int i = 0; i < net.weights.Length; i++)
                net.weights[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * range;
            return net;
        }

        void EnsureScratch()
        {
            if (activations != null && activations.Length == layers.Length) return;
            activations = new float[layers.Length][];
            for (int i = 0; i < layers.Length; i++) activations[i] = new float[layers[i]];
        }

        /// Propagacion hacia delante. Devuelve el buffer interno de salida: no
        /// lo guardes entre llamadas, se reescribe.
        public float[] Evaluate(float[] inputs)
        {
            EnsureScratch();

            var input = activations[0];
            int n = Mathf.Min(inputs.Length, input.Length);
            for (int i = 0; i < n; i++) input[i] = inputs[i];

            int w = 0;
            for (int layer = 1; layer < layers.Length; layer++)
            {
                var prev = activations[layer - 1];
                var cur = activations[layer];
                for (int j = 0; j < cur.Length; j++)
                {
                    float sum = weights[w + prev.Length]; // sesgo
                    for (int i = 0; i < prev.Length; i++)
                        sum += prev[i] * weights[w + i];
                    w += prev.Length + 1;
                    // tanh acota la salida a [-1, 1]: sirve igual para mover
                    // (direccion) que para puntuar acciones.
                    cur[j] = (float)Math.Tanh(sum);
                }
            }
            return activations[layers.Length - 1];
        }

        // ----------------------------------------------------------------
        //  Operadores geneticos
        // ----------------------------------------------------------------

        public NeuralNet Clone()
        {
            return new NeuralNet
            {
                layers = (int[])layers.Clone(),
                weights = (float[])weights.Clone(),
            };
        }

        /// Muta una fraccion de los pesos con ruido gaussiano.
        public void Mutate(System.Random rng, float rate = 0.15f, float strength = 0.35f)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                if (rng.NextDouble() > rate) continue;
                weights[i] = Mathf.Clamp(weights[i] + Gaussian(rng) * strength, -6f, 6f);
            }
        }

        /// Cruce uniforme: cada peso viene de uno de los dos padres.
        public static NeuralNet Crossover(NeuralNet a, NeuralNet b, System.Random rng)
        {
            var child = a.Clone();
            for (int i = 0; i < child.weights.Length && i < b.weights.Length; i++)
                if (rng.NextDouble() < 0.5) child.weights[i] = b.weights[i];
            return child;
        }

        static float Gaussian(System.Random rng)
        {
            // Box-Muller.
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
        }

        // ----------------------------------------------------------------
        //  Persistencia
        // ----------------------------------------------------------------

        public string ToJson() => JsonUtility.ToJson(this);

        public static NeuralNet FromJson(string json)
        {
            var net = JsonUtility.FromJson<NeuralNet>(json);
            return net != null && net.layers != null && net.weights != null ? net : null;
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < layers.Length; i++)
            {
                if (i > 0) sb.Append('-');
                sb.Append(layers[i]);
            }
            sb.Append(" (").Append(weights.Length).Append(" pesos)");
            return sb.ToString();
        }
    }
}
