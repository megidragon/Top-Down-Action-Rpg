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
            ["class.mage"] = ("Mago", "Mage"),
            ["class.key1"] = ("Tecla 1", "Key 1"),
            ["class.key2"] = ("Tecla 2", "Key 2"),
            ["class.key3"] = ("Tecla 3", "Key 3"),
            ["class.key4"] = ("Tecla 4", "Key 4"),
            ["class.key5"] = ("Tecla 5", "Key 5"),

            // HUD y juego
            ["hud.controls"] = (
                "WASD mover  |  Shift dash  |  Click Izq. atacar  |  Click Der. especial  |  Espacio parry / curar / hielo  |  1-4 objetos  |  E interactuar  |  C/V ordenes aliados",
                "WASD move  |  Shift dash  |  Left click attack  |  Right click special  |  Space parry / heal / ice  |  1-4 items  |  E interact  |  C/V ally orders"),
            ["zone.town"] = ("Ciudad", "Town"),
            ["zone.camp"] = ("Campamento - Nivel {0}", "Camp - Level {0}"),
            ["zone.level"] = ("Bosque - Nivel {0}", "Forest - Level {0}"),
            ["splash.level"] = ("NIVEL {0}", "LEVEL {0}"),
            ["splash.town"] = ("LA CIUDAD", "THE TOWN"),
            ["splash.camp"] = ("CAMPAMENTO", "REST CAMP"),
            ["exit.enter_forest"] = ("ENTRAR AL BOSQUE", "ENTER THE FOREST"),
            ["exit.next"] = ("SIGUIENTE NIVEL", "NEXT LEVEL"),
            ["exit.rest"] = ("SEGUIR ADENTRANDOSE", "VENTURE DEEPER"),
            ["exit.locked"] = ("(bloqueado)", "(locked)"),
            ["msg.clean"] = ("¡NIVEL LIMPIO!", "LEVEL CLEARED!"),
            ["msg.death"] = ("HAS MUERTO EN EL BOSQUE\nLa run termina aqui.\nPulsa R para reintentar",
                "YOU DIED IN THE FOREST\nThe run ends here.\nPress R to retry"),
            ["msg.victory"] = ("¡HAS ENCONTRADO EL TESORO DEL BOSQUE!\n\nLa leyenda era cierta.\nPulsa R para una nueva expedicion",
                "YOU FOUND THE FOREST TREASURE!\n\nThe legend was true.\nPress R for a new expedition"),
            // Texto flotante al beber un elixir
            ["fx.strength"] = ("+{0} FUERZA", "+{0} STRENGTH"),
            ["fx.defense"] = ("+{0} DEFENSA", "+{0} DEFENSE"),
            ["fx.speed"] = ("+{0} VELOCIDAD", "+{0} SPEED"),
            ["fx.energy"] = ("+{0} ENERGIA", "+{0} ENERGY"),

            // Controles tactiles (movil)
            ["touch.attack"] = ("ATQ", "ATK"),
            ["touch.special"] = ("ESP", "SPC"),
            ["touch.parry"] = ("PARRY", "PARRY"),
            ["touch.dash"] = ("DASH", "DASH"),
            ["touch.interact"] = ("USAR", "USE"),
            ["touch.ally_attack"] = ("¡AL\nATAQUE!", "ALL\nOUT!"),
            ["touch.ally_flee"] = ("HUID", "FLEE"),
            ["settings.touch"] = ("Controles tactiles", "Touch controls"),

            // Escena de pruebas (Lab)
            ["lab.title"] = ("LAB · Coliseo", "LAB · Colosseum"),
            ["lab.keys"] = (
                "F1-F5  invocar enemigo (guerrero/lancero/arquero/monje/mago)\nF6  limpiar enemigos\nF7  curar grupo\nF8  invocar aliado\nF9  torneo de IAs (guerrero)\nF10  liga: 5 clases x 6 algoritmos",
                "F1-F5  spawn enemy (warrior/lancer/archer/monk/mage)\nF6  clear enemies\nF7  heal party\nF8  spawn ally\nF9  combat AI tournament"),

            ["hint.rest"] = ("[E] descansar", "[E] rest"),
            ["hint.buy"] = ("[E] comprar", "[E] buy"),

            // Nombres de lo que vende el mercader
            ["item.potion_small"] = ("Pocion pequena", "Small potion"),
            ["item.potion_medium"] = ("Pocion mediana", "Medium potion"),
            ["item.potion_large"] = ("Pocion grande", "Large potion"),
            ["item.elixir_strength"] = ("Elixir de fuerza", "Strength elixir"),
            ["item.elixir_defense"] = ("Elixir de defensa", "Defense elixir"),
            ["item.elixir_speed"] = ("Elixir de velocidad", "Speed elixir"),
            ["item.elixir_energy"] = ("Elixir de energia", "Energy elixir"),
            ["hint.recruit"] = ("[E] reclutar", "[E] recruit"),
            ["ally.free"] = ("GRATIS", "FREE"),
            ["msg.treasure"] = ("¡EL TESORO DEL BOSQUE ES TUYO! +{0} monedas\nLa leyenda era cierta... y el bosque continua.",
                "THE FOREST TREASURE IS YOURS! +{0} coins\nThe legend was true... and the forest goes on."),
            ["msg.ally.attack"] = ("¡Aliados: A POR ELLOS!", "Allies: ATTACK!"),
            ["msg.ally.flee"] = ("¡Aliados: RETIRADA!", "Allies: RETREAT!"),
            ["msg.ally.down"] = ("Tu aliado ha caido...", "Your ally has fallen..."),

            // Panel de estadisticas
            ["stats.health"] = ("Vida", "Health"),
            ["stats.energy"] = ("Energia", "Energy"),
            ["stats.mana"] = ("Mana", "Mana"),
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
