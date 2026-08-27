using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg.AI
{
    /// Orquesta la escena de entrenamiento: sin jugador ni HUD de juego, solo
    /// la camara libre, las arenas y los marcadores. Lleva VARIAS poblaciones a
    /// la vez (una por clase), cada una con su propio bloque de arenas, y las
    /// arranca todas al entrar en Play. Los mejores quedan guardados en disco
    /// en cada record, asi que parar nunca pierde nada.
    public class TrainingDirector : MonoBehaviour
    {
        public ParallelTrainer[] trainers;
        public EvolutionTrainer[] evolutions;
        public Text overlayText;
        public Text hintText;

        TextMesh[][] arenaLabels; // [bloque][arena]

        IEnumerator Start()
        {
            // Sin esto, Unity congela el Play al perder el foco de la ventana
            // y el entrenamiento se para en cuanto miras otra cosa. Solo se
            // activa aqui: en el juego real, pausarse al perder el foco es lo
            // correcto.
            Application.runInBackground = true;

            yield return null; // deja correr los Awake de la escena

            var cam = Camera.main;
            if (cam != null)
            {
                var free = cam.GetComponent<FreeCameraController>();
                if (free == null) free = cam.gameObject.AddComponent<FreeCameraController>();
                FrameAllBlocks(free);
            }

            if (hintText != null)
                hintText.text = "WASD / flechas mover camara  ·  rueda zoom  ·  arrastrar con boton derecho";

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arenaLabels = new TextMesh[trainers.Length][];
            for (int b = 0; b < trainers.Length; b++)
            {
                // Toda la poblacion a la vista: el proposito de la escena es mirar.
                trainers[b].visibleArenas =
                    Mathf.Max(trainers[b].visibleArenas, evolutions[b].populationSize);

                BuildBlockTitle(font, b);
                BuildArenaLabels(font, b, evolutions[b].populationSize);
                StartCoroutine(evolutions[b].RunForever());
            }
        }

        /// Encuadra TODOS los bloques de arenas, contando con la relacion de
        /// aspecto (el tamano ortografico es media ALTURA de pantalla).
        void FrameAllBlocks(FreeCameraController free)
        {
            Vector2 min = trainers[0].Center(0), max = min;
            for (int b = 0; b < trainers.Length; b++)
                for (int i = 0; i < evolutions[b].populationSize; i++)
                {
                    var c = trainers[b].Center(i);
                    min = Vector2.Min(min, c);
                    max = Vector2.Max(max, c);
                }
            Vector2 mid = (min + max) * 0.5f;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7778f;
            float spanX = max.x - min.x + ParallelTrainer.ArenaSpacing;
            float spanY = max.y - min.y + ParallelTrainer.ArenaSpacing;
            float size = Mathf.Max(spanY, spanX / Mathf.Max(0.1f, aspect)) * 0.55f;
            free.Activate(mid, Mathf.Max(12f, size));
        }

        /// Nombre de la clase en grande sobre su bloque de arenas.
        void BuildBlockTitle(Font font, int b)
        {
            Vector2 min = trainers[b].Center(0), max = min;
            for (int i = 0; i < evolutions[b].populationSize; i++)
            {
                var c = trainers[b].Center(i);
                min = Vector2.Min(min, c);
                max = Vector2.Max(max, c);
            }

            var go = new GameObject($"BlockTitle_{b}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3((min.x + max.x) * 0.5f,
                max.y + trainers[b].arenaRadius + 9f, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 96;
            tm.characterSize = 0.75f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.75f, 0.95f, 1f, 1f);
            tm.text = EvolutionTrainer.ClassNames[evolutions[b].trainClass].ToUpper();

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = 30010;
        }

        /// Un rotulo flotante sobre cada arena: generacion y contra quien pelea
        /// esa red ahora mismo.
        void BuildArenaLabels(Font font, int b, int count)
        {
            arenaLabels[b] = new TextMesh[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"ArenaLabel_{b}_{i}");
                go.transform.SetParent(transform, false);
                Vector2 c = trainers[b].Center(i);
                go.transform.position = new Vector3(c.x, c.y + trainers[b].arenaRadius + 2.2f, 0f);

                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                // Grande a proposito: la vista por defecto abarca la rejilla
                // entera y un texto a escala de personaje seria ilegible.
                tm.fontSize = 64;
                tm.characterSize = 0.3f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 0.95f, 0.75f, 0.95f);

                var mr = go.GetComponent<MeshRenderer>();
                mr.material = font.material;
                mr.sortingOrder = 30010; // por encima de barras y unidades
                arenaLabels[b][i] = tm;
            }
        }

        float refreshTimer;

        void Update()
        {
            // El texto no necesita ir a frecuencia de frame, y a 8x menos aun.
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.25f;

            if (arenaLabels != null)
                for (int b = 0; b < trainers.Length; b++)
                {
                    if (arenaLabels[b] == null || evolutions[b].Generation == 0) continue;
                    for (int i = 0; i < arenaLabels[b].Length; i++)
                        if (arenaLabels[b][i] != null)
                            arenaLabels[b][i].text =
                                $"Gen {evolutions[b].Generation} · #{i}\nvs {trainers[b].ArenaLabel(i)}";
                }

            if (overlayText == null || trainers.Length == 0) return;

            int totalDuels = 0;
            float started = float.MaxValue;
            bool anyRunning = false;
            var sb = new System.Text.StringBuilder(512);
            sb.AppendLine("ENTRENAMIENTO NEUROEVOLUTIVO — 5 CLASES EN PARALELO");

            for (int b = 0; b < evolutions.Length; b++)
            {
                var e = evolutions[b];
                if (!e.Running) continue;
                anyRunning = true;
                totalDuels += trainers[b].DuelsCompleted;
                if (e.StartedRealtime < started) started = e.StartedRealtime;

                string record = e.BestFitnessEver > float.MinValue
                    ? $"{e.BestFitnessEver,5:F0} (g{e.BestGeneration})" : "    -";
                sb.AppendLine(
                      $"{EvolutionTrainer.ClassNames[e.trainClass],-8}  gen {e.Generation,3}"
                    + $"  ·  record {record}  ·  vict {e.LastWinRate,4:P0}"
                    + $"  ·  sin decision {e.LastTimeoutRate,4:P0}"
                    + $"  ·  {e.SavedCount} guardados");
            }

            if (!anyRunning)
            {
                overlayText.text = "ENTRENAMIENTO NEUROEVOLUTIVO\npreparando...";
                return;
            }

            float elapsed = Mathf.Max(0.001f, Time.realtimeSinceStartup - started);
            sb.AppendLine($"Rivales: {evolutions[0].RivalPoolLabel}");
            sb.Append($"Ritmo total: {totalDuels / elapsed:F1} duelos/s");
            overlayText.text = sb.ToString();
        }
    }
}
