using System.Collections.Generic;
using UnityEngine;

public class SlopeSpawner : MonoBehaviour
{
    [Header("Spawn Line")]
    public Transform spawnLineStart;
    public Transform spawnLineEnd;

    [Header("Prefabs")]
    public List<GameObject> obstaclePrefabs;

    [Header("Timing")]
    public float initialDelay = 1f;
    public float spawnInterval = 0.8f;
    public Vector2 intervalJitter = new Vector2(-0.3f, 0.3f);

    [Header("Limits & Cleanup")]
    public int maxAlive = 60;
    public float autoDestroyAfter = 12f;
    public float killBelowY = -20f;

    private float _timer;

    void Start()
    {
        _timer = initialDelay;
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            if (obstaclePrefabs.Count > 0 && transform.childCount < maxAlive)
                SpawnOne();

            _timer = spawnInterval + Random.Range(intervalJitter.x, intervalJitter.y);
        }

        // Basit temizlik
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.position.y < killBelowY)
            {
                Destroy(child.gameObject);
            }
        }
    }

    void SpawnOne()
    {
        float t = Random.value;
        Vector3 pos = Vector3.Lerp(spawnLineStart.position, spawnLineEnd.position, t);

        var prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Count)];
        var go = Instantiate(prefab, pos, Quaternion.identity, transform);

        // Güvenlik: rigidbody yoksa ekle
        var rb = go.GetComponent<Rigidbody>();
        if (!rb) rb = go.AddComponent<Rigidbody>();

        // Otomatik yok etme
        go.AddComponent<SelfDestroy>().Init(autoDestroyAfter);
    }

    private class SelfDestroy : MonoBehaviour
    {
        float _life;
        public void Init(float life) { _life = life; }
        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
