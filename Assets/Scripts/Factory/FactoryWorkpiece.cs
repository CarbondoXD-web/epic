using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FactoryWorkpiece : MonoBehaviour
{
    public string Id;
    public FactoryPartState State;
    public int StationId=-1;
    public GameObject blank,shaft;
    public Transform Visual => blank.activeSelf?blank.transform:shaft.transform;
    public void Finished(){blank.SetActive(false);shaft.SetActive(true);}
}
