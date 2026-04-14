using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paneles del Menú")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;

    public void OpenOptions()
    {
        mainMenuPanel.SetActive(false); 
        optionsPanel.SetActive(true);   
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);  
        mainMenuPanel.SetActive(true);
    }
}