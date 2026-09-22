using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetButton : MonoBehaviour
{
    public void RestartGame()
    {
        SessionLogger.Instance?.LogReset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}