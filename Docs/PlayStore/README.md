# Ficha de Google Play — CyberChimps

Assets gráficos listos para subir + la parte de texto, que es la que realmente
pesa en el posicionamiento.

---

## ⚠️ Primero, una aclaración importante sobre el "algoritmo"

**Play Store no lee las imágenes.** El buscador indexa **texto**: título,
descripción corta y descripción larga. El ranking después se mueve por señales
de comportamiento: instalaciones, **retención**, desinstalaciones, valoraciones
y crashes.

Entonces, ¿para qué sirven las imágenes? Para la **tasa de conversión**
(cuánta gente que ve la ficha la instala). Y ahí está el truco: la conversión
**sí** alimenta el ranking, porque Google prioriza fichas que convierten mejor
para la misma búsqueda.

> Resumen: las imágenes de esta carpeta están optimizadas para **convertir**.
> Para **posicionar**, lo que hay que trabajar es el texto de la sección 4.

---

## 1. Archivos incluidos

| Archivo | Medida | Dónde va en Play Console |
|---|---|---|
| `icon-512x512.png` | 512×512 | Ficha de Store → **Icono de la app** |
| `feature-graphic-1024x500.png` | 1024×500 | Ficha de Store → **Gráfico de la función** |
| `screenshots/01-art.png` … `05-timer.png` | 1920×1080 | Ficha de Store → **Capturas de teléfono** |

Todos son PNG de 24 bits **sin canal alfa** (Play rechaza transparencia en el
gráfico de la función) y pesan menos de 700 KB, muy por debajo de los límites.

El juego es **horizontal**, así que las capturas son 16:9 apaisadas. Play pide
**mínimo 2** y acepta **hasta 8**.

---

## 2. Orden de las capturas (esto importa)

En el listado, la mayoría de la gente **solo ve las primeras 2 o 3** sin
scrollear. El orden actual va de mayor a menor gancho:

1. **Jugá de a dos, online** → el diferencial del juego, va primero sí o sí
2. **Resolvé puzzles en equipo** → refuerza el co-op
3. **Juntá todos los Cyberdatos** → progresión/coleccionables
4. **Tres vidas, cero excusas** → desafío
5. **Corré contra el reloj** → rejugabilidad

Criterios de diseño aplicados: un solo mensaje por imagen, texto corto y
grande (legible en miniatura), y la misma identidad visual en las cinco para
que se lean como un set.

---

## 3. Lo que falta para maximizar conversión

Estas imágenes son promocionales y usan arte real del juego (los chimps del
banner y los íconos de UI). Funcionan, pero **Google y los usuarios prefieren
ver gameplay**. La mejora más grande que podés hacer:

- Sacá 2 o 3 capturas reales del juego (co-op con los dos monos, un puzzle,
  el HUD con el timer) y **reemplazá las posiciones 2 y 3**. Dejá la 1 como
  portada promocional.
- Para capturar: `adb shell screencap -p /sdcard/s.png && adb pull /sdcard/s.png`
- **Video promocional (YouTube)**: es el único elemento que supera a las
  capturas en conversión. 15-30 segundos, gameplay directo, sin intro larga.

---

## 4. Texto de la ficha (esto SÍ es el algoritmo)

### Título — máximo 30 caracteres
```
CyberChimps: Co-op Online
```
25 caracteres. Mete la keyword fuerte (**co-op online**) sin caer en spam,
que Play penaliza.

### Descripción corta — máximo 80 caracteres
Es lo que se ve sin desplegar y **pesa mucho** en la indexación:
```
Plataformas cooperativo online para 2. Puzzles, monedas y carreras contra reloj.
```
80 caracteres exactos.

### Descripción larga — máximo 4000 caracteres

Repetir las keywords principales **3-5 veces de forma natural** a lo largo del
texto. Las que conviene trabajar:

`cooperativo` · `co-op online` · `2 jugadores` · `plataformas` · `puzzles`
· `multijugador` · `para jugar con amigos`

Borrador para arrancar:

> **CyberChimps** es un juego de **plataformas cooperativo** donde dos monos
> tienen que escapar de una simulación creada por una IA.
>
> **Jugá con un amigo, online.** Creá una sala, pasale el código y listo:
> ya están los dos adentro. Cada nivel está pensado para **2 jugadores** que
> se coordinan: botones que hay que pisar al mismo tiempo, cajas que se empujan
> entre los dos y puertas que no se abren solo.
>
> **Controles simples.** Joystick para moverte, un botón para saltar y otro
> para agarrar, lanzar o empujar. Se aprende en diez segundos.
>
> **Corré contra el reloj.** Cada nivel se puntúa con estrellas según el tiempo
> y los Cyberdatos que junten. Siempre hay una ruta mejor para bajar el tiempo.
>
> **Qué te vas a encontrar:**
> · Modo **cooperativo online** para **2 jugadores**
> · Niveles de **plataformas** y **puzzles** en equipo
> · Coleccionables escondidos y rutas opcionales
> · Sistema de vidas y estrellas para rejugar
> · Gratis, con partidas cortas ideales para el celular
>
> ¿Te la bancás de a dos? Descargá **CyberChimps** y jugá **con amigos** ahora.

**Errores a evitar:** no repitas la misma keyword veinte veces (Play lo detecta
y penaliza), no menciones otras marcas ni pongas "#1" o "el mejor juego", y no
prometas nada que el juego no haga todavía.

---

## 5. Después de publicar

- **Experimentos de ficha de Store** (Play Console → Crecimiento): permite testear
  A/B el ícono, el gráfico de la función y las capturas contra la versión actual.
  Es gratis y te dice con datos reales cuál convierte mejor. Vale mucho más que
  cualquier suposición de diseño.
- Lo que más mueve el ranking a mediano plazo es la **retención** y las
  **valoraciones**. Un pedido de review bien puesto (después de completar un
  nivel, nunca al abrir) mueve la aguja más que cualquier imagen.

---

## Regenerar las imágenes

Se generan por script con Pillow a partir del arte del proyecto
(`Assets/CyberChimpsBanner.png`, `Assets/CyberChimpsIcon.png` y los íconos del
pack Belevich). Si cambia el arte o los textos, hay que volver a correr el
generador que quedó en el scratchpad de la sesión (`make_store.py`).
