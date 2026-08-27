using UnityEngine;

namespace TinyRpg
{
    /// Compuerta compartida para la tecla E: cada interactuable (mercader,
    /// fogata, reclutador...) sondea el teclado por su cuenta, y con radios
    /// solapados una sola pulsacion dispararia varios a la vez (doble cobro).
    /// El primero que consume la pulsacion en un frame gana; el resto espera.
    public static class InteractGate
    {
        static int consumedFrame = -1;

        public static bool TryConsume()
        {
            if (consumedFrame == Time.frameCount) return false;
            consumedFrame = Time.frameCount;
            return true;
        }
    }
}
