using UnityEngine;

/// <summary>
/// Versión offline del MobileUIConnector.
/// Colocar en el mismo Canvas que MobileUIConnector.
///
/// SETUP en Unity:
///   - Botón de salto (PointerDown) → MobileUIConnectorOffline.OnJumpDown()
///   - Botón de salto (PointerUp)   → MobileUIConnectorOffline.OnJumpUp()
///   - Botón de acción (Click)      → MobileUIConnectorOffline.OnActionPressed()
///
/// El Canvas puede tener AMBOS conectores al mismo tiempo:
///   - MobileUIConnector         → redirige al jugador online (en red)
///   - MobileUIConnectorOffline  → redirige al PlayerOfflineController
/// Los botones llaman a ambos — el que no tenga instancia activa no hace nada.
/// </summary>
public class MobileUIConnectorOffline : MonoBehaviour
{
    public void OnJumpDown()      => PlayerOfflineController.StaticJumpDown();
    public void OnJumpUp()        => PlayerOfflineController.StaticJumpUp();
    public void OnActionPressed() => PlayerOfflineController.StaticAction();
}
