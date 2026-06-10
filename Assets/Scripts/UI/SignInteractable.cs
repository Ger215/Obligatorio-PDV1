using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cartel interactuable: cuando el player está cerca muestra un prompt
/// ("Presiona E") y al apretar E abre/cierra un panel desplegable con info.
/// Necesita un Collider2D como trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SignInteractable : MonoBehaviour
{
    [Header("Contenido")]
    [TextArea(2, 6)]
    [SerializeField] private string message = "Escribe tu mensaje aquí.";

    [Header("Prompt (opcional)")]
    [Tooltip("GameObject que se muestra cuando el player está cerca (ej. un 'Presiona E' sobre el cartel). Dejar vacío si no querés prompt.")]
    [SerializeField] private GameObject promptObject;

    private bool playerInRange;
    private bool isOpen;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = true;
        if (promptObject != null) promptObject.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = false;
        if (promptObject != null) promptObject.SetActive(false);
        if (isOpen) Close();
    }

    private void Update()
    {
        if (!playerInRange) return;

        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
        if (!pressed) return;

        if (isOpen) Close();
        else Open();
    }

    private void Open()
    {
        if (SignUI.Instance == null)
        {
            Debug.LogWarning($"[SignInteractable:{name}] No hay SignUI en la escena. Agregá el componente SignUI a un GameObject.", this);
            return;
        }
        SignUI.Instance.Show(message);
        isOpen = true;
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void Close()
    {
        if (SignUI.Instance != null) SignUI.Instance.Hide();
        isOpen = false;
        if (promptObject != null && playerInRange) promptObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (isOpen) Close();
    }
}
