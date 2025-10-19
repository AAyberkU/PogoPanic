using UnityEngine;
using UnityEngine.UI; // UI elemanları için
using TMPro; // TextMeshPro için (eğer kullandıysanız)

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuPanel; // Menü panelini buraya sürükleyeceğiz
    public static bool isPaused = false; // Oyunun duraklatılıp duraklatılmadığını kontrol eder

    // --- MOUSE İMPLEMETASYONU ---
    void ShowMouse()
    {
        Cursor.lockState = CursorLockMode.None; // Fareyi serbest bırak
        Cursor.visible = true; // Fare imlecini göster
    }

    void HideMouse()
    {
        Cursor.lockState = CursorLockMode.Locked; // Fareyi ekranın ortasına kilitle
        Cursor.visible = false; // Fare imlecini gizle
    }
    // --- MOUSE İMPLEMETASYONU SONU ---


    void Start()
    {
        pauseMenuPanel.SetActive(false);
        isPaused = false;
        HideMouse(); // Oyun başladığında fareyi gizle
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        pauseMenuPanel.SetActive(true); // Paneli göster
        isPaused = true; // Oyunu duraklatıldı olarak işaretle
        ShowMouse(); // Fareyi göster
    }

    public void ResumeGame()
    {
        pauseMenuPanel.SetActive(false); // Paneli gizle
        isPaused = false; // Oyunu duraklatılmadı olarak işaretle
        HideMouse(); // Fareyi gizle
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}