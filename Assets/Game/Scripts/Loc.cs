using System;
using System.Collections.Generic;

namespace TinyRpg
{
    /// Localizacion minima ES/EN. Loc.T(clave) devuelve el texto en el idioma
    /// activo (GameSettings.Language). Los textos dinamicos usan string.Format.
    public static class Loc
    {
        public static event Action LanguageChanged;

        static readonly Dictionary<string, (string es, string en)> table =
            new Dictionary<string, (string, string)>
        {
            // Pantalla de titulo
            ["title.game"] = ("EL TESORO DEL BOSQUE", "THE FOREST TREASURE"),
            ["title.start"] = ("Comenzar partida", "Start game"),
            ["title.settings"] = ("Configuracion", "Settings"),
            ["title.quit"] = ("Salir del juego", "Quit game"),

            // Configuracion
            ["set.title"] = ("CONFIGURACION", "SETTINGS"),
            ["set.tab.video"] = ("Video", "Video"),
            ["set.tab.audio"] = ("Sonido", "Sound"),
            ["set.tab.general"] = ("General", "General"),
            ["set.resolution"] = ("Resolucion", "Resolution"),
            ["set.windowmode"] = ("Modo de pantalla", "Screen mode"),
            ["set.fullscreen"] = ("Pantalla completa", "Fullscreen"),
            ["set.windowed"] = ("Ventana", "Windowed"),
            ["set.shake"] = ("Temblor de pantalla", "Screen shake"),
            ["set.on"] = ("Activado", "On"),
            ["set.off"] = ("Desactivado", "Off"),
            ["set.language"] = ("Idioma", "Language"),
            ["set.vol.general"] = ("Volumen general", "Master volume"),
            ["set.vol.effects"] = ("Efectos", "Effects"),
            ["set.vol.music"] = ("Musica", "Music"),
            ["set.back"] = ("Volver", "Back"),

            // Pausa
            ["pause.title"] = ("PAUSA", "PAUSED"),

            // Seleccion de clase
            ["class.title"] = ("ELIGE TU CLASE", "CHOOSE YOUR CLASS"),
            ["class.warrior"] = ("Guerrero", "Warrior"),
            ["class.lancer"] = ("Lancero", "Lancer"),
            ["class.archer"] = ("Arquero", "Archer"),
            ["class.monk"] = ("Monje", "Monk"),
            ["class.key1"] = ("Tecla 1", "Key 1"),
            ["class.key2"] = ("Tecla 2", "Key 2"),
            ["class.key3"] = ("Tecla 3", "Key 3"),
            ["class.key4"] = ("Tecla 4", "Key 4"),

            // HUD y juego
            ["hud.controls"] = (
                "WASD mover  |  Shift dash  |  Click Izq. atacar  |  Click Der. especial  |  Espacio parry o curar  |  1-4 objetos  |  E interactuar",
                "WASD move  |  Shift dash  |  Left click attack  |  Right click special  |  Space parry or heal  |  1-4 items  |  E interact"),
            ["zone.town"] = ("Ciudad", "Town"),
            ["zone.camp"] = ("Campamento", "Camp"),
            ["zone.level"] = ("Bosque - Nivel {0}", "Forest - Level {0}"),
            ["exit.enter_forest"] = ("ENTRAR AL BOSQUE", "ENTER THE FOREST"),
            ["exit.next"] = ("SIGUIENTE NIVEL", "NEXT LEVEL"),
            ["exit.rest"] = ("SEGUIR ADENTRANDOSE", "VENTURE DEEPER"),
            ["exit.locked"] = ("(bloqueado)", "(locked)"),
            ["msg.clean"] = ("¡NIVEL LIMPIO!", "LEVEL CLEARED!"),
            ["msg.death"] = ("HAS MUERTO EN EL BOSQUE\nLa run termina aqui.\nPulsa R para reintentar",
                "YOU DIED IN THE FOREST\nThe run ends here.\nPress R to retry"),
            ["msg.victory"] = ("¡HAS ENCONTRADO EL TESORO DEL BOSQUE!\n\nLa leyenda era cierta.\nPulsa R para una nueva expedicion",
                "YOU FOUND THE FOREST TREASURE!\n\nThe legend was true.\nPress R for a new expedition"),
            ["hint.rest"] = ("[E] descansar", "[E] rest"),
            ["hint.buy"] = ("[E] comprar", "[E] buy"),

            // Panel de estadisticas
            ["stats.health"] = ("Vida", "Health"),
            ["stats.energy"] = ("Energia", "Energy"),
            ["stats.strength"] = ("Fuerza", "Strength"),
            ["stats.defense"] = ("Defensa", "Defense"),
            ["stats.speed"] = ("Velocidad", "Speed"),
            ["stats.zone"] = ("Zona", "Zone"),
            ["stats.none"] = ("Sin personaje", "No character"),
        };

        public static string T(string key)
        {
            if (!table.TryGetValue(key, out var entry)) return key;
            return GameSettings.Language == GameLanguage.English ? entry.en : entry.es;
        }

        public static void NotifyLanguageChanged() => LanguageChanged?.Invoke();
    }
}
