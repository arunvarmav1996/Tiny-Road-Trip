using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner spawnerInstance;
    [Header("Vehicles")]
    public List<GameObject> vehicleList;
    public int vehicleCount = 12;
    public GameObject obstaclePrefab;
    public Transform vehicleParents;

    public void Awake()
    {
        if(spawnerInstance == null)
        {
            spawnerInstance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(obj: this);
        }
    }

    public void Start()
    {
        GenerateObjects();
    }
    private void GenerateObjects()
    {
        for (int i = 0; i < vehicleCount; i++)
        {
            GenerateNewVehicle();
        }
    }

    private void GenerateNewVehicle()
    {
        GameObject vehicleCreated = Instantiate(obstaclePrefab, Vector2.zero, Quaternion.identity, vehicleParents);
        vehicleCreated.SetActive(false);
        vehicleList.Add(vehicleCreated);
    }

    public GameObject GetVehicle(Vector3 spawnLocation)
    {
        foreach (var vehicle in vehicleList)
        {
            if (!vehicle.activeSelf)
            {
                vehicle.transform.position = spawnLocation;
                vehicle.SetActive(true);
                return vehicle;
            }
        }
        GenerateNewVehicle();
        vehicleList[vehicleList.Count - 1].transform.position = spawnLocation;
        vehicleList[vehicleList.Count - 1].SetActive(true);
        return vehicleList[vehicleList.Count - 1];

    }

    public void DisableAllObjects()
    {
        foreach (GameObject item in vehicleList)
        {
            item.SetActive(false);
        }
    }
}
