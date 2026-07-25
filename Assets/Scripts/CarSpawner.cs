using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private Material[] carMaterials;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 5f;
    [SerializeField] private float carSpeed = 8f;

    private float timer;
    private float nextSpawnTime;

    private void Start()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void Update()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || carPrefab == null) return;

        timer += Time.deltaTime;
        if (timer >= nextSpawnTime)
        {
            timer = 0f;
            nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            SpawnCar();
        }
    }

    private void SpawnCar()
    {
        var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var car = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);

        var obstacle = car.GetComponent<CarObstacle>();
        if (obstacle != null) obstacle.Init(carSpeed);

        if (carMaterials != null && carMaterials.Length > 0)
        {
            var carRenderer = car.GetComponentInChildren<Renderer>();
            if (carRenderer != null) carRenderer.material = carMaterials[Random.Range(0, carMaterials.Length)];
        }
    }
}
