using System.Collections.Generic;
using UnityEngine;

public class TrackConcreatGap : MonoBehaviour
{
    [System.Serializable]
    public class GapData
    {
        public string path;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
    }

    public List<GapData> gapObjects = new List<GapData>()
    {
        // =====================================================
        // Track sider
        // =====================================================

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track sider/Rectangle009",
            position = new Vector3(3.2029f, -0.15955f, 0.00051f),
            rotation = new Vector3(-89.98f, 0f, -180f),
            scale = new Vector3(1f, 1f, 0.5854985f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track sider/Rectangle010",
            position = new Vector3(0.86598f, -0.15904f, 0.001f),
            rotation = new Vector3(90f, 0f, -180f),
            scale = new Vector3(-1f, -1f, -0.585025f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track sider/Rectangle011",
            position = new Vector3(-0.91698f, -0.15903f, 0.00101f),
            rotation = new Vector3(-89.98f, 0f, -180f),
            scale = new Vector3(1f, 1f, 0.5850258f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track sider/Rectangle012",
            position = new Vector3(-3.23199f, -0.15958f, 0.001f),
            rotation = new Vector3(90f, 0f, -180f),
            scale = new Vector3(-1f, -1f, -0.5854952f)
        },

        // =====================================================
        // Track support concrete
        // =====================================================

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track suport concrit/Line009",
            position = new Vector3(2.8925f, 0.5f, 0.0599f),
            rotation = new Vector3(90f, 0f, 0f),
            scale = new Vector3(1f, 1f, 0.6592532f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track suport concrit/Line013",
            position = new Vector3(1.176f, 0.5f, 0.06f),
            rotation = new Vector3(-90f, 0f, 0f),
            scale = new Vector3(-1f, -1f, -0.6592532f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track suport concrit/Line014",
            position = new Vector3(-1.227f, 0.5f, 0.06f),
            rotation = new Vector3(90f, 0f, 0f),
            scale = new Vector3(1f, 1f, 0.6592532f)
        },

        new GapData()
        {
            path = "1 meter track  rail sepret mesh/Track suport concrit/Line015",
            position = new Vector3(-2.922f, 0.5f, 0.06f),
            rotation = new Vector3(-90f, 0f, 0f),
            scale = new Vector3(-1f, -1f, -0.6592532f)
        }
    };

    [ContextMenu("Apply Transform Properties")]
    public void ApplyAllTransforms()
    {
        foreach (GapData data in gapObjects)
        {
            ApplyTransform(data);
        }

        Debug.Log("All Transform Properties Applied");
    }

    [ContextMenu("Add Gap")]
    public void AddGap()
    {
        ApplyAllTransforms();
    }

    private void ApplyTransform(GapData data)
    {
        Transform target = transform.Find(data.path);

        if (target == null)
        {
            Debug.LogError("Not Found : " + data.path);
            return;
        }

        target.localPosition = data.position;
        target.localEulerAngles = data.rotation;
        target.localScale = data.scale;
    }
}