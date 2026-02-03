using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class Arrow
{
    private string _name;
    private GameObject _parent;
    private GameObject _tail;
    private GameObject _head;
    private Vector3 _arrowheadDirection;
    private float _currentTailLength;
    private Vector3 _lastParentPosition;
    private Quaternion lastParentRotation;
    private Vector3 _lastParentScale;
    private float _lastParentArea;
    private float _relativeSize;
    private Vector3 _headBaseScale;
    private Vector3 _tailBaseScale;
    private float _labelScale;
    private Quaternion _labelRotation;
    private Vector3 _labelOffst;


    // Label (optional)
    private GameObject _label3D;

    // New helper variables for the label fix
    private Vector3 _labelOffset;
    private Vector3 _labelBaseScale;

    // Constructor
    public Arrow(
        GameObject parent,
        Vector3 initialDirection = default,
        float lengthOfTail = 1,
        float relativeSize = 1f,
        Material headMaterial = null,
        Material tailMaterial = null,
        GameObject label3D = null,
        float? labelScale = null,
        Quaternion? labelRotation = null,
        Vector3? labelOffst = null,
        string name = null
    )
    {
        _parent = parent;

        // --------------------------------------------
        // Initialize Arrowhead Direction
        _arrowheadDirection = (initialDirection == default) ? _parent.transform.eulerAngles : initialDirection;

        // --------------------------------------------
        // Locate Script Folder and Prefabs
        //        string scriptFolderPath = Path.GetDirectoryName(RootPath);





        //        //string headPrefabPath = Path.Combine(scriptFolderPath, "headPrefab.prefab");
        //        //string tailPrefabPath = Path.Combine(scriptFolderPath, "tailPrefab.prefab");

        //        GameObject headPrefab = null;
        //        GameObject tailPrefab = null;
        //        _labelOffst = labelOffst ?? Vector3.zero;

        //        try
        //        {
        //#if UNITY_EDITOR
        //            //headPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(headPrefabPath);
        //            GameObject headPrefabPath = Resources.Load<GameObject>("headPrefab.prefab");
        //            //tailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tailPrefabPath);
        //            GameObject tailPrefabPath = Resources.Load<GameObject>("tailPrefab.prefab");

        //#endif

        //            if (headPrefab == null)
        //            {
        //                headPrefab = CreateFallbackHead();
        //            }

        //            if (tailPrefab == null)
        //            {
        //                tailPrefab = CreateFallbackTail(); // Fixed Typo: Was assigning to headPrefab in previous snippet
        //            }
        //        }
        //        catch (System.Exception e)
        //        {
        //            Debug.LogError("Error loading prefabs: " + e.Message);
        //            headPrefab = CreateFallbackHead();
        //            tailPrefab = CreateFallbackTail();
        //        }

        // --------------------------------------------
        // Locate Script Folder and Prefabs
        // (We don't need RootPath or Path.GetDirectoryName anymore)

        GameObject headPrefab = null;
        GameObject tailPrefab = null;
        _labelOffst = labelOffst ?? Vector3.zero;

        try
        {
            // FIX 1: No #if UNITY_EDITOR. We need this in the build too!
            // FIX 2: No ".prefab" extension.
            // FIX 3: Assign to 'headPrefab', not a new variable named 'headPrefabPath'.

            headPrefab = Resources.Load<GameObject>("headPrefab");
            tailPrefab = Resources.Load<GameObject>("tailPrefab");

            if (name != null)
            {
                if (headPrefab != null) headPrefab.name = name + "_Head";
                if (tailPrefab != null) tailPrefab.name = name + "_Tail";
            }

            if (headPrefab == null)
            {
                // This logs if it can't find it (Check your folder spelling!)
                Debug.LogWarning("Could not find 'headPrefab' in any Resources folder. Using fallback.");
                headPrefab = CreateFallbackHead();
            }

            if (tailPrefab == null)
            {
                Debug.LogWarning("Could not find 'tailPrefab' in any Resources folder. Using fallback.");
                tailPrefab = CreateFallbackTail();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error loading prefabs: " + e.Message);
            headPrefab = CreateFallbackHead();
            tailPrefab = CreateFallbackTail();
        }

        GameObject head_ = GameObject.Instantiate(headPrefab);
        GameObject tail_ = GameObject.Instantiate(tailPrefab);

        // --------------------------------------------
        // Set Parent-Child Hierarchy
        head_.transform.SetParent(parent.transform, false);
        tail_.transform.SetParent(parent.transform, false);

        _head = head_;
        _tail = tail_;

        // --------------------------------------------
        // Modified Label Setup for Fix
        // --------------------------------------------
        _labelScale = labelScale ?? 1f;
        _labelRotation = labelRotation ?? Quaternion.identity;

        if (label3D != null)
        {
             _label3D = label3D;

            _label3D.name = "ArrowLabel";

            // 2. Parent to ROOT, not Head (Prevents squashing)
            //if (_parent != null) _label3D.transform.SetParent(_parent.transform, true);

            // 3. Cache Base Scale
            _labelBaseScale = _label3D.transform.localScale;

            // 4. Calculate Offset
            float headSize = 0f;
            var r = _head.GetComponent<Renderer>();
            if (r != null) headSize = r.bounds.extents.magnitude;

            // Offset 0.2 units above the head radius
            _labelOffset = new Vector3(headSize,0f, 0f) + _labelOffst;
        }

        // Ensure local position/rotation are clean so subsequent calculations are stable
        _head.transform.localPosition = Vector3.zero;
        _head.transform.localRotation = Quaternion.identity;
        _tail.transform.localPosition = Vector3.zero;
        _tail.transform.localRotation = Quaternion.identity;

        // Cache original prefab scales so we can rebuild them in world space without distortion
        _headBaseScale = _head.transform.localScale;
        _tailBaseScale = _tail.transform.localScale;

        // --------------------------------------------
        // Initialize Other Attributes
        _currentTailLength = lengthOfTail;
        _arrowheadDirection = initialDirection;
        _lastParentPosition = _parent.transform.position;
        lastParentRotation = _parent.transform.rotation;
        _lastParentScale = _parent.transform.localScale;
        _lastParentArea = 0;
        _relativeSize = Mathf.Max(relativeSize, 0.0001f);

        // --------------------------------------------
        // Apply scaling
        ApplyHeadScale();
        ApplyTailScale();

        // Initial Label Update
        UpdateLabel();

        // --------------------------------------------
        // Setting the material
        try
        {
            if (headMaterial != null && _head.GetComponent<Renderer>() != null)
            {
                _head.GetComponent<Renderer>().material = headMaterial;
            }

            if (tailMaterial != null && _tail.GetComponent<Renderer>() != null)
            {
                _tail.GetComponent<Renderer>().material = tailMaterial;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error applying materials: " + e.Message);
        }

        Debug.Log("Arrow constructor completed successfully.");
    }

    // ------------------------------------------------------------
    // The new Label Update Logic
    // ------------------------------------------------------------
    private void UpdateLabel()
    {
        if (_label3D == null || _head == null) return;

        // Follow Head Position + Rotated Offset
        _label3D.transform.position = _head.transform.position + (_head.transform.rotation * _labelOffset);

        // Follow Head Rotation + Custom Rotation
        _label3D.transform.rotation = _head.transform.rotation * _labelRotation;

        // Maintain Scale (Ignore Parent Squash)
        Vector3 targetScale = _labelBaseScale * _relativeSize * _labelScale;
        Vector3 parentScale = ResolveParentScale(_label3D.transform);
        _label3D.transform.localScale = DivideVector3(targetScale, parentScale);
    }

    // ------------------------------------------------------------
    // Fallback geometry

    private GameObject CreateFallbackHead()
    {
        GameObject cone = new GameObject("FallbackArrowHead");
        MeshFilter mf = cone.AddComponent<MeshFilter>();
        MeshRenderer mr = cone.AddComponent<MeshRenderer>();

        Mesh coneMesh = new Mesh();

        Vector3[] vertices = new Vector3[5];
        vertices[0] = new Vector3(0, 0.5f, 0);      // Tip
        vertices[1] = new Vector3(0.1f, 0, 0.1f);   // Base
        vertices[2] = new Vector3(0.1f, 0, -0.1f);
        vertices[3] = new Vector3(-0.1f, 0, -0.1f);
        vertices[4] = new Vector3(-0.1f, 0, 0.1f);

        int[] triangles = new int[12];
        triangles[0] = 0; triangles[1] = 1; triangles[2] = 2;
        triangles[3] = 0; triangles[4] = 2; triangles[5] = 3;
        triangles[6] = 0; triangles[7] = 3; triangles[8] = 4;
        triangles[9] = 0; triangles[10] = 4; triangles[11] = 1;

        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();

        mf.mesh = coneMesh;

        // FIXED: Explicitly set Material Color
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        mr.material = mat;

        return cone;
    }

    private GameObject CreateFallbackTail()
    {
        GameObject cylinder = new GameObject("FallbackArrowTail");
        MeshFilter mf = cylinder.AddComponent<MeshFilter>();
        MeshRenderer mr = cylinder.AddComponent<MeshRenderer>();

        Mesh cylinderMesh = new Mesh();

        Vector3[] vertices = new Vector3[8];
        vertices[0] = new Vector3(0.02f, 0.5f, 0.02f);
        vertices[1] = new Vector3(0.02f, 0.5f, -0.02f);
        vertices[2] = new Vector3(-0.02f, 0.5f, -0.02f);
        vertices[3] = new Vector3(-0.02f, 0.5f, 0.02f);

        vertices[4] = new Vector3(0.02f, -0.5f, 0.02f);
        vertices[5] = new Vector3(0.02f, -0.5f, -0.02f);
        vertices[6] = new Vector3(-0.02f, -0.5f, -0.02f);
        vertices[7] = new Vector3(-0.02f, -0.5f, 0.02f);

        int[] triangles = new int[24];
        triangles[0] = 0; triangles[1] = 1; triangles[2] = 5;
        triangles[3] = 0; triangles[4] = 5; triangles[5] = 4;
        triangles[6] = 1; triangles[7] = 2; triangles[8] = 6;
        triangles[9] = 1; triangles[10] = 6; triangles[11] = 5;
        triangles[12] = 2; triangles[13] = 3; triangles[14] = 7;
        triangles[15] = 2; triangles[16] = 7; triangles[17] = 6;
        triangles[18] = 3; triangles[19] = 0; triangles[20] = 4;
        triangles[21] = 3; triangles[22] = 4; triangles[23] = 7;

        cylinderMesh.vertices = vertices;
        cylinderMesh.triangles = triangles;
        cylinderMesh.RecalculateNormals();

        mf.mesh = cylinderMesh;

        // FIXED: Explicitly set Material Color
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        mr.material = mat;

        return cylinder;
    }

    // ------------------------------------------------------------
    // RootPath

    public static string RootPath
    {
#if UNITY_EDITOR
        get
        {
            var g = AssetDatabase.FindAssets($"t:Script {nameof(Arrow)}");
            if (g.Length == 0)
            {
                Debug.LogError("Cannot find Arrow script in project!");
                return "Assets";
            }
            string path = AssetDatabase.GUIDToAssetPath(g[0]);
            return path;
        }
#else
        get { return "Assets"; }
#endif
    }

    // ------------------------------------------------------------
    // Public API (unchanged)

    public void SetParent(GameObject parent)
    {
        _parent = parent;
        if (_head != null) _head.transform.SetParent(parent != null ? parent.transform : null, true);
        if (_tail != null) _tail.transform.SetParent(parent != null ? parent.transform : null, true);

        // Ensure label follows hierarchy changes
        if (_label3D != null) _label3D.transform.SetParent(parent != null ? parent.transform : null, true);

        ApplyHeadScale();
        ApplyTailScale();
    }

    public void SetInitialDirection(Vector3 direction) => _arrowheadDirection = direction;

    public void SetTailLength(float length)
    {
        if (length >= 0)
        {
            _currentTailLength = length;
            ApplyTailScale();
        }
    }

    public void UpdateParentTransform()
    {
        _lastParentPosition = _parent.transform.position;
        lastParentRotation = _parent.transform.rotation;
        _lastParentScale = _parent.transform.localScale;
    }

    public void SetParentArea(float area) => _lastParentArea = area;

    public bool IsParentTransformChanged()
    {
        if (_parent != null)
        {
            return _lastParentPosition != _parent.transform.position ||
                   lastParentRotation != _parent.transform.rotation ||
                   _lastParentScale != _parent.transform.localScale;
        }
        return false;
    }

    public void SetScene()
    {
        UpdateTail();
        UpdateHead();
        UpdateLabel(); // Sync label
        EnsureHeadIsEnabled();
    }

    public void Update()
    {
        try
        {
            UpdateHead();
            UpdateTail();
            UpdateLabel(); // Sync label
            EnsureHeadIsEnabled();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in Arrow.Update(): " + e.Message);
        }
    }

    public void SetHeadColor(Color color)
    {
        try
        {
            if (_head != null && _head.GetComponent<Renderer>() != null)
            {
                Material newMaterial = new Material(_head.GetComponent<Renderer>().material);
                newMaterial.color = color;
                _head.GetComponent<Renderer>().material = newMaterial;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in SetHeadColor(): " + e.Message);
        }
    }

    public void ResetHeadColor(Material originalMaterial)
    {
        try
        {
            if (_head != null && _head.GetComponent<Renderer>() != null)
            {
                _head.GetComponent<Renderer>().material = originalMaterial;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in ResetHeadColor(): " + e.Message);
        }
    }

    // ------------------------------------------------------------
    // Internals

    private void UpdateTail()
    {
        try
        {
            if (_tail == null || _parent == null) return;

            _tail.transform.rotation = _parent.transform.rotation;
            _tail.transform.position = CalculateTailOffsetPosition();
            ApplyTailScale();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in UpdateTail(): " + e.Message);
        }
    }

    public static float CalculateLengthByValue(float value)
    {
        if (value > 0)
        {
            float maxLength = 10f;
            float length = (maxLength / 100) * value;
            return length;
        }
        return 0;
    }

    private void ApplyHeadScale()
    {
        if (_head == null) return;

        Vector3 desiredWorldScale = _headBaseScale * _relativeSize;
        Vector3 parentScale = ResolveParentScale(_head.transform);
        _head.transform.localScale = DivideVector3(desiredWorldScale, parentScale);
    }

    private void ApplyTailScale()
    {
        if (_tail == null) return;

        Vector3 desiredWorldScale = new Vector3(
            _tailBaseScale.x * _relativeSize,
            _currentTailLength,
            _tailBaseScale.z * _relativeSize);

        Vector3 parentScale = ResolveParentScale(_tail.transform);
        _tail.transform.localScale = DivideVector3(desiredWorldScale, parentScale);
    }

    private Vector3 ResolveParentScale(Transform child)
    {
        if (child != null && child.parent != null)
        {
            Vector3 scale = child.parent.lossyScale;
            if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
            if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
            if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
            return scale;
        }
        return Vector3.one;
    }

    private static Vector3 DivideVector3(Vector3 numerator, Vector3 denominator)
    {
        return new Vector3(
            SafeDivide(numerator.x, denominator.x),
            SafeDivide(numerator.y, denominator.y),
            SafeDivide(numerator.z, denominator.z));
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        if (Mathf.Approximately(denominator, 0f)) return numerator;
        return numerator / denominator;
    }

    private Vector3 CalculateTailOffsetPosition()
    {
        try
        {
            if (_tail == null) return _parent != null ? _parent.transform.position : Vector3.zero;

            float halfLength = _currentTailLength * 0.5f;
            Vector3 direction = _tail.transform.rotation * Vector3.up;
            Vector3 parentPosition = _parent != null ? _parent.transform.position : Vector3.zero;
            Vector3 offset = direction * halfLength;
            return parentPosition + offset;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error calculating tail position: " + e.Message);
            return _parent != null ? _parent.transform.position : Vector3.zero;
        }
    }

    public Vector3 CalculateHeadOffsetPosition()
    {
        try
        {
            if (_tail == null) return _parent != null ? _parent.transform.position : Vector3.zero;

            Vector3 direction = _tail.transform.rotation * Vector3.up;
            float halfLength = _currentTailLength * 0.5f;
            Vector3 offset = direction * halfLength;
            return _tail.transform.position + offset;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error calculating head position: " + e.Message);
            return _parent != null ? _parent.transform.position : Vector3.zero;
        }
    }

    private void UpdateHead()
    {
        try
        {
            if (_head == null || _tail == null) return;

            EnsureHeadIsEnabled();
            ApplyHeadScale();

            _head.transform.position = CalculateHeadOffsetPosition();

            if (_currentTailLength < 0)
                _head.transform.rotation = Quaternion.AngleAxis(180, Vector3.up) * _tail.transform.rotation;
            else
                _head.transform.rotation = _tail.transform.rotation;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in UpdateHead(): " + e.Message);
        }
    }

    private void EnsureHeadIsEnabled()
    {
        try
        {
            if (_head == null || _tail == null) return;

            bool active = !Mathf.Approximately(_currentTailLength, 0f);

            if (active)
            {
                _head.SetActive(true);
            }
            else
            {
                _head.SetActive(false);
            }

            // Ensure Label visibility toggles with head
            if (_label3D != null && _label3D.activeSelf != active)
            {
                _label3D.SetActive(active);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error in EnsureHeadIsEnabled(): " + e.Message);
        }
    }

    // Unused helpers kept from your original file
    private Vector3 GetTailEndWorldPosition(Bounds tailBounds)
    {
        Vector3 tailEndLocalPosition = tailBounds.center + new Vector3(0, 0, tailBounds.extents.z);
        return _tail.transform.TransformPoint(tailEndLocalPosition);
    }

    private Vector3 GetParentTopWorldPosition(Bounds parentBounds)
    {
        Vector3 parentTopLocalPosition = parentBounds.center + new Vector3(0, parentBounds.extents.y, 0);
        return _parent.transform.TransformPoint(parentTopLocalPosition);
    }

    private Vector3 GetArrowBottomWorldPosition(Bounds tailBounds)
    {
        Vector3 arrowBottomLocalPosition = tailBounds.center - new Vector3(0, tailBounds.extents.y, 0);
        return _tail.transform.TransformPoint(arrowBottomLocalPosition);
    }
}