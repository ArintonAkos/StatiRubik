using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/**
 * Sample script to demonstrate how you can initialize a cube of a specific size and expose/listen to different cube events.
 */
public class RubikCubeInitializer : MonoBehaviour
{

    [Header ("General")]

    [SerializeField] private RubikCube _rubikCubePrefab = null;
    
    private readonly int _dimensions = 3;

    [Tooltip("Prefab 1")]
    [SerializeField] 
    private GameObject _cubletPrefab1 = null;
    [Tooltip("Prefab 2")]
    [SerializeField]
    private GameObject _cubletPrefab2 = null;
    [Tooltip("Prefab 3")]
    [SerializeField]
    private GameObject _cubletPrefab3 = null;
    [Tooltip("Prefab 4")]
    [SerializeField]
    private GameObject _cubletPrefab4 = null;
    [Tooltip("Prefab 5")]
    [SerializeField]
    private GameObject _cubletPrefab5 = null;
    [Tooltip("Prefab 6")]
    [SerializeField]
    private GameObject _cubletPrefab6 = null;


    [Tooltip("Normally the side pieces of a real cube don't have stickers, would you like to hide those here as well?")]
    [SerializeField] 
    private bool hideInvisibleSides = true;
    
    [Tooltip("What is our max history size? Set to 0 to disable the history.")]
    [SerializeField] 
    private int _maxHistorySize = 50;

    [Header("Shuffle settings")]

    [SerializeField] 
    private bool _shuffleOnStart = true;

    [SerializeField] 
    private int _shuffleCount = 20;

    [SerializeField] 
    private float _shuffleSpeed = 2.5f;

    // Reference to the actual cube and disc rotation script so that we can disable it when we solved the cube
    private RubikCube _rubikCube = null;
    private DiscRotator _discRotator = null;

    [Header("Respawn settings")]
    [Tooltip("Can we press the 2,3,4 - 9 keys to spawn a new cube of a different size?")]
    [SerializeField] 
    private bool _allowRespawning = true;

    [Tooltip("How many units should we zoom in or out extra per cube unit? (We zoom out as we increase the cube size to make sure it still fits on the screen.)")]
    [SerializeField] 
    private float _zoomFactorPerUnit = 1;
    
    [SerializeField] 
    private CameraMouseOrbit _cameraMouseOrbit = null;
    
    private float _baseDistance;

    [Header("Debug settings")]
    [SerializeField] 
    private AxisDisplay _axisDisplay = null;

    [Header("Debug settings")]
    [SerializeField] 
    private Text _debugText = null;

    // Allow generic event handling
    [Serializable]
    public class RubikCubeEvent : UnityEvent<RubikCube> { }

    [Header("Cube events")]

    public RubikCubeEvent OnNewCubeBeforeInitialize;
    public RubikCubeEvent OnNewCubeAfterInitialize;
    public RubikCubeEvent OnCurrentCubeBeforeDestroy;
    public UnityEvent OnCurrentCubeAfterDestroy;

    // Called when any disc changes on the cube
    public RubikCubeEvent OnCubeChanged;
    public RubikCubeEvent OnCubeSolved;

    private List<GameObject> _cubletPrefabs;
    private DiceCube _cube;
    private int TEST_SIZE = 50000;

    // Start is called before the first frame update
    void Start()
    {
        _cubletPrefabs = new()
        { 
            _cubletPrefab1,
            _cubletPrefab2,
            _cubletPrefab3,
            _cubletPrefab4,
            _cubletPrefab5,
            _cubletPrefab6
        };

        if (_cameraMouseOrbit != null)
		{
            // Set up the basic zoom distance
            _baseDistance = _cameraMouseOrbit.targetDistance - RubikCube.Dimensions * _zoomFactorPerUnit;
		}

        _cube = new DiceCube();
        
        StartCoroutine(SpawnNewCube());

        new Thread(() =>
        {
            int okCount = 0;

            for (int i = 0; i < TEST_SIZE; i++)
            {
                DiceCube cube = new DiceCube();
                bool completesRequirement = cube.GenerateRandomPath()
                    .Rotate(_shuffleSpeed)
                    .GetDotCountsOnDisks()
                    .FindAll(v => v == 4)
                    .Count()
                    .Equals(3);

                if (completesRequirement)
                {
                    okCount++;
                }
            }

            double avg = (double)okCount / TEST_SIZE;

            Debug.Log("Az esely " + TEST_SIZE + " db tesztesetre: " + avg);
        }).Start();
    }

	private IEnumerator SpawnNewCube()
	{
        DestroyCurrentCubeIfPresent();
        
        _rubikCube = Instantiate(_rubikCubePrefab, transform);
        _discRotator = _rubikCube.GetComponent<DiscRotator>();
        _cube.SetCube(_rubikCube);

        // Make sure we show the local axis of the rubikcube in the top right
        if (_axisDisplay != null)
        {
            _axisDisplay.copyFrom = _rubikCube.transform;
        }

        OnNewCubeBeforeInitialize?.Invoke(_rubikCube);
        
        _rubikCube.Initialize(_cubletPrefabs, _maxHistorySize, hideInvisibleSides);

        if (_cameraMouseOrbit != null)
		{
            _cameraMouseOrbit.targetDistance = _baseDistance + RubikCube.Dimensions * _zoomFactorPerUnit;
		}

        int count = _cube.GenerateRandomPath()
            .Rotate(_shuffleSpeed, true)
            .GetDotCountsOnDisks()
            .FindAll(v => v == 4)
            .Count();

        yield return new WaitForSeconds(4);

        if (count != 3)
        {
            StartCoroutine(SpawnNewCube());
        }
    }

    private void DestroyCurrentCubeIfPresent()
	{
        if (_rubikCube != null)
		{
            _rubikCube.OnChanged -= OnCubeChangedCallback;
            _rubikCube.OnSolved -= OnCubeSolvedCallback;

            OnCurrentCubeBeforeDestroy?.Invoke(_rubikCube);
            Destroy(_rubikCube.gameObject);
            OnCurrentCubeAfterDestroy?.Invoke();
		}
	}

    private void OnCubeChangedCallback()
    {
        Debug.Log("Cube changed");
        OnCubeChanged?.Invoke(_rubikCube);
    }

    private void OnCubeSolvedCallback()
    {
        Debug.Log("Cube solved");
        OnCubeSolved?.Invoke(_rubikCube);
    }

	private void OnDrawGizmos()
	{
        Gizmos.DrawCube(transform.position, Vector3.one * _dimensions * 0.9f);
	}

    public void SetDiscRotationEnabled(bool pAllowDiscRotation)
	{
        if (_rubikCube != null)
		{
            _rubikCube.GetComponent<DiscRotator>().enabled = pAllowDiscRotation;
		}
	}

    public void SetCubeRotationEnabled(bool pAllowCubeRotation)
    {
        if (_rubikCube != null)
        {
            _rubikCube.GetComponent<CubeRotator>().enabled = pAllowCubeRotation;
        }
    }

	private void Update()
	{
        if (_debugText != null && _discRotator != null)
        {
            _debugText.text = _discRotator.GetDebugInfo() + "\n" + Application.platform;

        }

        if (_allowRespawning && Input.anyKeyDown && Input.inputString.Length == 1)
        {
            if (Input.inputString == "r")
            {
                StartCoroutine(SpawnNewCube());
            }
		}
	}

}
