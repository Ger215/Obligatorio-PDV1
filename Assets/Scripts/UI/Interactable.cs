using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Interactable : MonoBehaviour
{
    [Header("Configuración Visual")]
    [Tooltip("GameObject del prompt (ej. 'Presiona E').")]
    [SerializeField] private GameObject promptObject;

    [Header("Eventos")]
    [Tooltip("¿Qué pasa cuando el jugador aprieta la E?")]
    public UnityEvent onInteract;

    private bool playerInRange;

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
        // Usa tu mismo chequeo del PlayerController
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = true;
        if (promptObject != null) promptObject.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInRange = false;
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void Update()
    {
        if (!playerInRange) return;

        bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
        
        if (pressed)
        {
            // Ejecuta lo que sea que le hayas configurado en el Inspector
            onInteract?.Invoke();
            
            // Opcional: Ocultar el prompt tras interactuar
            if (promptObject != null) promptObject.SetActive(false); 
        }
    }
}