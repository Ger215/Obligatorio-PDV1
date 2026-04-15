using UnityEngine;
using UnityEngine.UI; // Necesario para cambiar los sprites de los botones
using UnityEngine.Audio;

public class MainMenuManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer mainMixer;

    [Header("Imágenes de los Botones")]
    public Image musicButtonImage;
    public Image sfxButtonImage;

    [Header("Sprites (Íconos)")]
    public Sprite iconMusicOn;
    public Sprite iconMusicOff;
    public Sprite iconSFXOn;
    public Sprite iconSFXOff;

    // Variables para saber si están encendidos o apagados (arrancan encendidos)
    private bool isMusicOn = true;
    private bool isSFXOn = true;

    // --- FUNCIONES DE AUDIO ---

    public void ToggleMusic()
    {
        isMusicOn = !isMusicOn; // Invierte el estado (Si era true, pasa a false)
        
        // En el AudioMixer, 0f es volumen normal y -80f es silencio total
        mainMixer.SetFloat("MusicVol", isMusicOn ? 0f : -80f);
        
        // Cambia la imagen del botón según el estado
        musicButtonImage.sprite = isMusicOn ? iconMusicOn : iconMusicOff;
    }

    public void ToggleSFX()
    {
        isSFXOn = !isSFXOn; 
        
        mainMixer.SetFloat("SFXVol", isSFXOn ? 0f : -80f);
        
        sfxButtonImage.sprite = isSFXOn ? iconSFXOn : iconSFXOff;
    }

    // --- SALIR DEL JUEGO ---
    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }
}