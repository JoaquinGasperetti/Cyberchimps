using UnityEngine;

/// <summary>
/// Colocar este script en el GameObject del Canvas de controles móviles.
///
/// SETUP en Unity:
///   - Botón de salto (PointerDown) → MobileUIConnector.OnJumpDown()
///   - Botón de salto (PointerUp)   → MobileUIConnector.OnJumpUp()
///   - Botón de acción (Click)      → MobileUIConnector.OnActionPressed()
///
/// Este script redirige el input al PlayerInputHandler del jugador LOCAL,
/// sin importar cuántos jugadores haya spawneados en red.
/// </summary>
public class MobileUIConnector : MonoBehaviour
{
    public void OnJumpDown()     => PlayerInputHandler.StaticMobileJumpDown();
    public void OnJumpUp()       => PlayerInputHandler.StaticMobileJumpUp();
    public void OnActionPressed() => PlayerInputHandler.StaticTriggerAction();
}
