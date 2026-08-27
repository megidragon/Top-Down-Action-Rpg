# Sistema de combate y estadísticas — El Tesoro del Bosque

Paquete autocontenido con el combate cuerpo a cuerpo / a distancia, las
estadísticas (vida, energía, maná, atributos, equipos) y la IA de duelo del
juego "El Tesoro del Bosque". No depende de ningún otro sistema del juego
original: ni de su HUD, ni de su flujo de niveles, ni de su input.

## Requisitos

- Unity 6 (probado en 6000.0.74f1). Solo física 2D estándar; no exige URP ni
  el New Input System (el paquete no lee input: tu juego llama a sus métodos).

## Qué incluye

| Pieza | Scripts |
|---|---|
| Estadísticas | `CharacterStats`, `CharacterAttributes` |
| Movimiento | `CharacterMotor` (velocidad, dash, apuntado, retroceso) |
| Combate base | `CharacterCombat` (barrido, estocada, parry, aturdimiento) |
| Clases | `ArcherCombat`, `MageCombat`, `MonkCombat` (guerrero/lancero = base con otros alcances) |
| Proyectiles y áreas | `ArrowProjectile`, `ArrowStrike`, `MagicCircleBlast`, `IceSpikeField` |
| Efectos | `AttackVfx`, `VfxLibrary`, `FloatingText`, `SmoothCameraFollow` (sacudida incluida), `YSorter` |
| Visual de unidad | `UnitAnimator` (opcional), `WorldStatusBars` (opcional) |
| IA de duelo | `DuelistAI` (6 cerebros escritos + cerebro neuronal), `NeuralNet`, `TrainingMode` |

## Montaje mínimo de un combatiente

1. GameObject con `Rigidbody2D` (gravedad 0, rotación congelada) y un collider 2D.
2. Añade `CharacterStats`, `CharacterMotor` y un componente de combate
   (`CharacterCombat` para melee; o `ArcherCombat` / `MageCombat` / `MonkCombat`).
3. `stats.team`: 0 = jugador y aliados, 1 = enemigos. El daño ignora al propio equipo.
4. Tu código de input/IA conduce así:

```csharp
motor.SetMoveInput(direccion);      // -1..1 en cada eje
motor.AimDirection = haciaDondeMira;
motor.TryDash();                    // gasta energia

combat.AimPoint = puntoDelMundo;    // para proyectiles y areas
combat.OnPrimaryDown(dir);          // ataque principal
combat.OnPrimaryUp(dir);            // (arquero: soltar la lluvia)
combat.OnSecondaryDown(dir);        // ataque secundario
combat.OnSpecial(dir);              // especial (gasta mana si usesMana)
combat.TryParry(dir);               // bloqueo en cono
```

5. Recibir daño: `stats.Damage(cantidad, direccionDelGolpe)`. Eventos útiles:
   `stats.Damaged`, `stats.Died`, `stats.HealthChanged`, `combat.AttackStarted`,
   `combat.ParryPerformed`.

## IA lista para usar

Añade `DuelistAI` al combatiente, asigna `foe` (o llama `SetFoe`) y elige
`brain`: Rusher, Spacer, Counter, Feinter, Ambusher o Flanker. Con
`autoDrive = true` se mueve y pelea solo; con `false`, tu propia IA de
patrulla llama `ThinkOnce()` y lee `DesiredDistance` / `NeuralMove`.

- `reactionDelay` (0.18–0.34 s) es el retardo entre percibir y actuar: la IA
  actúa sobre lo que vio hace ese tiempo, nunca sobre el presente. Es lo que
  la hace justa contra personas; no lo pongas a 0.
- Cerebro neuronal: `brain = CombatBrain.Neural` y asigna `net`
  (`NeuralNet.FromJson(json)`). Los genomas entrenados del juego original
  sirven si la observación no cambió (valida `net.InputCount == DuelistAI.ObservationCount`).

## Piezas opcionales

- **VfxLibrary**: crea un GameObject con el componente y asigna material y
  sprites (flecha, proyectil mágico, círculo, pincho de hielo). Sin él, los
  ataques funcionan pero sin sus efectos.
- **UnitAnimator**: asigna `animator` y `spriteRenderer`. El combate lo usa si
  existe (`PlayAction(estado, duracion)` reproduce estados por nombre:
  "Attack", "Shoot", etc. según tu controller). Sin él, no hay animaciones
  pero el combate es correcto.
- **WorldStatusBars**: barras sobre la cabeza. Contrato: `healthFillAnchor` /
  `energyFillAnchor` / `manaFillAnchor` son transforms cuya escala X se
  escala 0..1; `manaBar` se oculta si la unidad no usa maná.
- **TrainingMode**: `TrainingMode.Begin(escala)` acelera el reloj y suprime
  todos los efectos (para simulaciones masivas); `End()` restaura. Con varios
  usuarios simultáneos, el reloj se restaura al salir el último.
- **SmoothCameraFollow**: seguimiento suave + `Shake(cantidad)` estático.
  `ShakeEnabled` lo apaga (conéctalo a tus ajustes).

## Notas

- Todo vive en los namespaces `TinyRpg` y `TinyRpg.AI`.
- `YSorter` ordena el sprite por Y (`sortingOrder = 10000 - 100*y`); si tu
  juego ya ordena de otra forma, no lo añadas a las unidades.
- Los textos del paquete (nombres de estados, comentarios) están en español.
