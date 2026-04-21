using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

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
    
    [Header("Paneles UI")]
    public GameObject howToPlayPanel;

    private bool isMusicOn = true;
    private bool isSFXOn = true;

    public void StartGame()
    {
        SceneManager.LoadScene("GameScene"); 
    }
    public void ToggleMusic()
    {
        isMusicOn = !isMusicOn; 
        
        mainMixer.SetFloat("MusicVol", isMusicOn ? 0f : -80f);
        
        musicButtonImage.sprite = isMusicOn ? iconMusicOn : iconMusicOff;
    }

    public void ToggleSFX()
    {
        isSFXOn = !isSFXOn; 
        
        mainMixer.SetFloat("SFXVol", isSFXOn ? 0f : -80f);
        
        sfxButtonImage.sprite = isSFXOn ? iconSFXOn : iconSFXOff;
    }

    public void OpenHowToPlay()
    {
        howToPlayPanel.SetActive(true); 
    }

    public void CloseHowToPlay()
    {
        howToPlayPanel.SetActive(false); 
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}