using System.Collections;
using System.Collections.Generic;
using UnityEngine;

  //[System.Serializable]
    public class PlateSurface: MonoBehaviour 
    {
        public Transform plate;
        public Renderer rendererOverride;

        [HideInInspector] public Vector3 center;
        [HideInInspector] public Vector2 extents;
        [HideInInspector] public bool isValid;
    }

