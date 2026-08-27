# 🏹 El Tesoro del Bosque

*Un Action RPG roguelike top-down hecho en Unity 6 con los assets de [Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords).*

Cuenta la leyenda que en el corazón del bosque más peligroso del reino duerme un tesoro sin dueño. Muchos entraron a buscarlo. Ninguno volvió. Elige tu clase, cruza la muralla de árboles y ábrete camino a través de **10 niveles** cada vez más hostiles hasta el corazón del bosque.

![Pantalla de título](Screenshots/titulo.png)

## ✨ Características

- **4 clases jugables**, cada una con su identidad y su propio kit
- **Combate de acción en tiempo real** con parry direccional, dash y gestión de energía
- **Roguelike**: 10 niveles de bosque con diseños únicos, dificultad creciente y muerte permanente
- **Campamentos de descanso** con fogata curativa y mercaderes de pociones y elixires
- **Sistema de estadísticas** (fuerza / defensa / velocidad) que crece durante la run
- Interfaz completa: menú, configuración (resolución, idioma **ES/EN**, volúmenes, temblor de cámara) y pausa
- Instalador para Windows

## ⚔️ Sistema de combate

![Combate en el bosque](Screenshots/combate.png)

Todo el combate gira alrededor del ratón: **apuntas donde miras**, y jugador y enemigos comparten las mismas reglas.

| Mecánica | Cómo funciona |
|---|---|
| **Ataque principal** (click izq.) | Barrido en abanico de **130°** centrado en el cursor, de corto alcance |
| **Ataque especial** (click der.) | Estocada en línea recta de mayor alcance (o el especial de cada clase) |
| **Parry** (Espacio) | Bloquea **un** golpe que llegue dentro de un cono de 60° hacia donde apuntas — el atacante bloqueado sale despedido y aturdido. ¡Bloquea también flechas! |
| **Dash** (Shift) | Impulso rápido en la dirección de movimiento |
| **Energía** | Cada ataque y cada dash cuestan 25 de una barra de 50 que se regenera sola: no puedes spamear |
| **Impactos** | Los golpes empujan al objetivo y sacuden la cámara (desactivable en opciones) |

### Las clases

![Selección de clase](Screenshots/seleccion-clase.png)

- 🗡️ **Guerrero** — el equilibrado: 150 de vida, espada rápida y fiable.
- 🔱 **Lancero** — alcance superior y más daño por golpe, pero su arma pesada tarda un 75% más en recuperarse entre ataques. 112 de vida.
- 🏹 **Arquero** — el cristal: 75 de vida. Mantén el click izquierdo para apuntar una **lluvia de flechas** en área (el objetivo se marca en el suelo medio segundo antes del impacto) y dispara **ráfagas de 3 flechas** con el derecho.
- ✊ **Monje** — el alborotador: sin parry, pero se **cura a sí mismo** (y a aliados cercanos) con Espacio, sus patadas mandan a volar a los enemigos y su **embestida** — redirigible en pleno vuelo — aturde al impactar. 125 de vida.

Los enemigos usan las cuatro clases con IA propia: los arqueros te telegrafían la lluvia de flechas y mantienen la distancia, los monjes se curan y embisten, los lanceros te sobrepasan en alcance.

## 🌲 La expedición

![Ciudad inicial](Screenshots/ciudad.png)

1. **La ciudad**: tu punto de partida, con sus vecinos trabajando. La entrada al bosque está marcada al norte.
2. **Niveles 1–10**: mapas cerrados por murallas de árboles — claros, estanques, ríos con vados, pantanos, laberintos de arboledas... Limpia el nivel para desbloquear la salida. Cada 3 niveles se suma un enemigo más.
3. **Campamentos** (tras los niveles 3, 6 y 9): descansa junto a la fogata (curación completa) y compra a los mercaderes — pociones de 1/2/3 usos (1/3/6 monedas) o **elixires permanentes** de fuerza, defensa o velocidad (4 monedas).
4. **El tesoro**: en el nivel 10 te espera la recompensa... si llegas.

Las monedas las sueltan los enemigos al morir y se recogen con solo acercarse. El inventario tiene 4 huecos (teclas 1–4) y las botellas de vida no se apilan: cada una ocupa su hueco con sus usos.

## 🎮 Controles

| Acción | Control |
|---|---|
| Moverse | `WASD` |
| Dash | `Shift` |
| Ataque principal / especial | `Click izq.` / `Click der.` |
| Parry o curación | `Espacio` |
| Usar objeto | `1`–`4` |
| Interactuar / comprar | `E` |
| Pausa | `ESC` |
| Reintentar tras morir | `R` |

## 🛠️ Desarrollo

- **Unity 6** (6000.0) con URP 2D e Input System.
- Todo el mundo se genera **en runtime**: la escena solo contiene los sistemas, y los 12 mapas (ciudad, 10 niveles y campamento) se pintan al vuelo con autotiling sobre el tileset de Tiny Swords.
- La escena se regenera por completo desde el menú **TinyRpg → Construir escena del juego**; el ejecutable con **TinyRpg → Compilar juego (Windows)** y el instalador con [Inno Setup](https://jrsoftware.org/isinfo.php) (`Installer/ElTesoroDelBosque.iss`).

## 🙏 Créditos

- **Arte**: [Tiny Swords](https://pixelfrog-assets.itch.io/tiny-swords) de **[Pixel Frog](https://pixelfrog-assets.itch.io/)** — un pack de assets excepcional. ¡Gracias!
- **Iconos de objetos**: *Tiny Fantasy Icons* de **Vespa Warrior** (Unity Asset Store).

## 📄 Licencia

El código fuente del proyecto se publica bajo licencia [MIT](LICENSE) — úsalo, apréndelo y modifícalo libremente. Los assets artísticos pertenecen a sus creadores y conservan sus propias licencias.
