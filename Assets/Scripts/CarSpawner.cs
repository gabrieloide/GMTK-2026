using UnityEngine;
using System.Collections.Generic;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject carPrefab;
    [SerializeField] private Material[] carMaterials;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float minSpawnInterval = 4f;
    [SerializeField] private float maxSpawnInterval = 9f;
    [SerializeField] private float carSpeed = 8f;
    [SerializeField] private int maxConcurrentCars = 4;

    private float timer;
    private float nextSpawnTime;
    private readonly List<GameObject> activeCars = new List<GameObject>();

    private void Start()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    private void Update()
    {
        if (spawnPoints == null || spawnPoints.Length == 0 || carPrefab == null) return;

        activeCars.RemoveAll(car => car == null);
        if (activeCars.Count >= maxConcurrentCars) return;

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
        activeCars.Add(car);

        var obstacle = car.GetComponent<CarObstacle>();
        if (obstacle != null) obstacle.Init(carSpeed);

        if (carMaterials != null && carMaterials.Length > 0)
        {
            var carRenderer = car.GetComponentInChildren<Renderer>();
            if (carRenderer != null) carRenderer.material = carMaterials[Random.Range(0, carMaterials.Length)];
        }
    }
}
