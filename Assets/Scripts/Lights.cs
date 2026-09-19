using System;
using System.Collections.Generic;
using UnityEngine;

public class Lights : MonoBehaviour
{
    public List<GameObject> LightObjs;


    private void Awake()
    {
        foreach (var lightObj in LightObjs)
        {
            lightObj.SetActive(false);
        }
    }

    public void SetData(int value)
    {
        value--;
        Debug.LogError($"{gameObject.name} :: {value}");
        for (var i = 0; i < LightObjs.Count; i++)
        {
            LightObjs[i].SetActive(i == value);
        }
    }
}