using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneVisibilityManager : MonoBehaviour
{
    [System.Serializable]
    public class SceneVisibilityRule
    {
        public string sceneName;
        public GameObject[] objectsToHide;
    }

    [SerializeField] private SceneVisibilityRule[] rules;

    void Start()
    {
        ApplyRules(SceneManager.GetActiveScene().name);
        SceneManager.sceneLoaded += (scene, mode) => ApplyRules(scene.name);
    }

    private void ApplyRules(string sceneName)
    {
        foreach (var rule in rules)
        {
            bool hide = sceneName == rule.sceneName;
            foreach (var obj in rule.objectsToHide)
                if (obj) obj.SetActive(!hide);
        }
    }
}