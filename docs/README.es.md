<div align="center">

# DialMenu

**El regulador de volumen de tu teclado se convierte en un menú.**

Un menú radial que convierte el mando giratorio de un teclado mecánico en un
lanzador completo de programas y macros.

![Plataforma](https://img.shields.io/badge/plataforma-Windows%2010%20%7C%2011-2f2f33?style=flat-square)
![macOS](https://img.shields.io/badge/macOS-previsto-c9c9cd?style=flat-square)
![Linux](https://img.shields.io/badge/Linux-previsto-c9c9cd?style=flat-square)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-2f2f33?style=flat-square)
![Licencia](https://img.shields.io/badge/licencia-MIT-2f2f33?style=flat-square)

<img src="../docs/ring-dark.png" width="300" alt="El anillo en modo oscuro">

[English](../README.md) · [Deutsch](README.de.md) · **Español** · [Русский](README.ru.md)

</div>

---

## De qué va esto

La mayoría de los teclados mecánicos tienen un mando giratorio cableado
directamente al volumen. DialMenu intercepta esas señales antes de que el
sistema las vea y abre un menú en su lugar: **gira para elegir, pulsa para
ejecutar.**

El teclado no se modifica nunca. Sin flashear, sin firmware, sin
controladores. DialMenu solo escucha: desconecta el teclado o cierra el
programa y todo vuelve a estar como antes.

```
   Girar ──▶ informe HID ──▶ DialMenu ──▶ acción
                                │
                                └──▶ el sistema ya no ve la tecla de volumen
```

## Funciones

<img src="../docs/settings.png" alt="La ventana de ajustes con la lista de comandos, el editor y la vista previa del anillo">

- **Menú radial**: superficies de vidrio, monocromo, con un único contorno de
  selección iluminado. Tantos comandos como quieras; el anillo crece con ellos.
- **Cuatro temas de anillo** en dos luminosidades cada uno, además de claro y
  oscuro a elección o según el ajuste del sistema.
- **Alemán e inglés**, conmutables sin reiniciar.
- **Programas, carpetas, sitios web**, incluida una selección entre los
  programas realmente instalados en lugar de escribir rutas.
- **Combinaciones de teclas**: `Ctrl+Alt+M`, `Win+D`, `Alt+F4` y todo lo
  intermedio.
- **Macros**: encadena combinaciones, texto y pausas en una secuencia.
- **Discord**: silenciar y ensordecer desde el anillo.
- **Varios perfiles** y **varios dispositivos**.
- **Detección automática del dispositivo**: tres giros a la derecha, tres a la
  izquierda, una pulsación. DialMenu encuentra el mando por sí solo sin que
  nadie introduzca un identificador.
- **Colocación libre**: doce posiciones más el monitor bajo el cursor.

## Instalación

Windows 10 u 11, .NET Framework 4.8 (preinstalado en Windows 11). No hay
instalador: DialMenu es un único `.exe`.

1. [Descarga la última versión](../../releases/latest)
2. Coloca `DialMenu.exe` en una carpeta a tu elección
3. Ejecútalo: se abre la ventana de ajustes

A partir de ahí bastan dos clics: en **Dispositivos**, pulsa *Iniciar
detección* y gira el mando tres pasos a la derecha, tres a la izquierda y
pulsa una vez. Después monta lo que quieras en el anillo en **Comandos**.

## Primeros pasos

**¿Qué teclados funcionan?**
Cualquiera cuyo mando envíe las teclas de volumen estándar, lo que cubre a la
mayoría. Gracias a la detección automática no hay que introducir nada.

**El mando no hace nada.**
Vuelve a ejecutar el asistente en **Dispositivos**. El indicador de progreso
("derecha 1/3 · izquierda 0/3") muestra si llega alguna señal. Si se queda en
cero, el teclado envía algo que no llega como tecla multimedia.

**¿Dónde están los ajustes si el mando ocupa el menú?**
En el área de notificación de la barra de tareas. Un clic allí abre la ventana.

**¿Puedo recuperar el volumen?**
Sí: cierra DialMenu desde el área de notificación o desmarca la casilla en
**Comportamiento**. Las teclas de volumen del teclado siguen funcionando en
cualquier caso.

## Configuración

Todo está en `%APPDATA%\DialMenu\config.json`, legible y versionable. Un perfil
se puede copiar y compartir.

## Compilar desde el código fuente

<img src="../docs/ring-light.png" width="260" align="right" alt="El anillo en modo claro">

Requisitos: Windows con .NET Framework 4.8 y MSBuild (incluido en Visual Studio
o en las Build Tools).

```powershell
.\build.ps1
```

El archivo final queda en `bin\Release\DialMenu.exe`.

Sin Visual Studio basta la copia de MSBuild que Windows ya trae:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe' DialMenu.csproj /p:Configuration=Release
```

## Cómo funciona

Dos mecanismos encajan entre sí:

**Raw Input** lee los paquetes HID en bruto. De ellos deduce qué paso se giró y
cuándo se pulsó el mando, independientemente de qué teclas haga el sistema con
ellos.

**Un hook de teclado** intercepta las teclas de volumen resultantes. Solo las
deja pasar cuando *no* vienen inmediatamente del propio dispositivo. Las teclas
de volumen de otros programas y teclados no se tocan.

Ambos corren en un hilo propio. La razón está medida: si el hook no responde en
300 ms, Windows descarta la tecla y el volumen cambia igualmente. Si corre en el
hilo de la interfaz, ocurre exactamente eso en cuanto se dibuja algo.

Las pulsaciones que genera el propio programa van marcadas y nunca se
interceptan; de lo contrario se bloquearía a sí mismo.

## Plataformas

| | Estado |
|---|---|
| Windows 10 / 11 | terminado |
| macOS | previsto |
| Linux | previsto |

Los dos mecanismos en los que se apoya DialMenu están atados a la plataforma y
no tienen equivalente directo en macOS ni en Linux. El camino hasta allí no es
por tanto un port, sino una segunda implementación: en Linux mediante `evdev` y
`uinput`, en macOS mediante `IOHIDManager` y `CGEventTap`. La rueda en sí, los
temas y la lógica de manejo no se ven afectados y se pueden reutilizar.

## Límites

- **Solo dispositivos con teclas multimedia estándar.** Un firmware con
  protocolo propio no se puede interceptar.
- **Un paso despierta el menú, todavía no elige.** De lo contrario el primer
  paso tendría doble función.
- **El volumen del sistema no está en el mando** mientras DialMenu se ejecuta.
  Las teclas de volumen del teclado siguen funcionando.
- **Las etiquetas del anillo** solo aparecen hasta seis entradas. Más allá no
  cabe ninguna palabra en el arco: quedan los iconos.

## Contribuir

Los informes de errores y las sugerencias son bienvenidos. Si un teclado no se
detecta, ayuda un fragmento del indicador en **Dispositivos**.

## Licencia

MIT: consulta [LICENSE](../LICENSE).
