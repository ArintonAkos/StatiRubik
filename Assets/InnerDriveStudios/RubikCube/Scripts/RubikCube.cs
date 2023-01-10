using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

/**
 * Creates a Rubik Cube of a given size with the specified 'Cublet' types,
 * that let's you rotate layers/discs of cublets in a certain direction.
 * 
 * The responsibilities of this class are:
 * - creating a cube and all it cublets
 * - rotating discs of the cube and keeping track of these 'moves'
 * - allowing you to undo a move
 * - shuffling a cube
 * 
 * Note that the actual initialization of the cube, in which you specify how big
 * the cube should be and which prefabs it should use for the cublets, should be done
 * through another script (for example using the RubikCubeInitializer or a custom script).
 * 
 * @author J.C. Wichman - InnerDriveStudios.com
 */
[RequireComponent(typeof(BoxCollider))]
public class RubikCube : MonoBehaviour
{
    // A cube can only be initialized once, if you want to reset a cube, destroy and create a new one.
    public bool IsInitialized { private set; get; }

    // Stores the cube dimensions after initializing (eg 3x3x3, 4x4x4 etc).
    public static readonly int Dimensions = 3;

    // Are we currently rotating? 
    // This is mostly used to prevent (and allow others to prevent) other actions on the cube in an invalid state.
    public bool IsDiscRotating { private set; get; }

    // The index of the far side element in the 3d cublet array in the x, y or z direction (equal to dimensions - 1).
    private int _border = -1;

    // Stores all instances of the created cublets in a 3D array backing store for quick access without raycasting.
    // Every disc rotation operation we do has to maintain the 'integrity' of this backing store.
    private Transform[,,] _cublets = null;

    // While rotating we need a temporary copy of the (cublets in the) disc we are rotating,
    // to avoid overwriting them (similar to using a temporary variable when swapping two items in a list)
    private Transform[,] _cubletBackingStore = null;

    // In order to easily rotate a disc of cublets we temporarily parent all the cublets we need to rotate to this pivot.
    private Transform _pivot = null;

    // We store a history of moves, so that you can easily backtrack/undo the moves you've made.
    // This history is implemented using using a ring buffer approach, in other words if your history
    // is 10 long, the 11th move 'wraps' around in the history array and overwrites the 1st move.
    // If you don't want to use a history, simply set its size to 0 during initialization.
    private Move[] _history;
    private int _currentHistoryIndex = 0;   //at which index do we store the next move
    private int _currentHistorySize = 0;    //this starts at 0 and has a max length of _history.Length

    // Required to select a random axis for shuffling, note that we only use 'positive' axis
    private readonly Vector3[] _axis = new Vector3[] { Vector3.right, Vector3.up, Vector3.forward };

    // Public events we can listen to, to learn more about the current state of the cube
    public event System.Action OnChanged;
    public event System.Action OnSolved;

    

    /**
     * Constructs a x by x by x cube where x == pDimensions, around the local origin, using pCubletPrefab
     * instances that are expected to be exactly 1 x 1 x 1 unit (or less if you like gaps). 
	 * In addition if pMaxHistorySize > 0 we also keep track of the move history so that we may undo moves,
	 * while a setting of pMaxHistorySize == 0, effectively disables the history.
     */
    public void Initialize(List<GameObject> pCubletPrefabs, int pMaxHistorySize, bool pTurnInvisibleCubletSidesOff = true)
    {
        if (IsInitialized)
        {
            throw new System.Exception("Initialized cube cannot be initialized again");
        }

        Assert.IsFalse(pCubletPrefabs.Count < 2, "The cublet prefab cannot be null");
        Assert.IsFalse(pMaxHistorySize < 0, "The max history cannot be smaller than 0");

        // Store some initial settings based on given parameters
        IsInitialized = true;
        _border = Dimensions - 1;
        _history = new Move[pMaxHistorySize];

        // Create a pivot that we can use to rotate a disc of cublets visually when we need to
        _pivot = new GameObject("Pivot").transform;
        _pivot.SetParent(transform, false);

        //initialize the 3d array to store all our cublets
        _cublets = new Transform[Dimensions, Dimensions, Dimensions];

        // Now go through the whole 3d array and create cublets for all (outer) elements.

        // We store the cublets in this order:
        // Given an D * D * D cube, where D is the dimension (or size) of the cube, 
        // the index of the bottom, left, back side element is 0,0,0
        // and the index of the top, right and front side is (dimensions-1, dimensions-1, dimensions-1)
        // or easier (_border, border, border).

        // Visually however we would like the center of the cube to be at the origin of its local coordinate system.
        // Given a 3 * 3 * 3 cube for example, the center of the cube would be at (0,0,0), and the cublets directly 
        // left and right of this center would be at (-1,0,0) and (1,0,0). 
        // This makes sense since the total distance from the center of the left cublet and right cublet is 2 units
        // (even though the cube itself is 3 * 3 * 3)).

        // Based on that we first calculate the position of the most bottom left backside cublet from the perspective
        // of looking at the xy axis (x right, y up) in the direction the +z axis is pointing.
        Vector3 bottomLeftBack = Vector3.one * -_border / 2.0f;


        GameObject[,,] _cubletIndexPrefabs = new GameObject[3, 3, 3];

        // Back Side
        _cubletIndexPrefabs[0, 0, 2] = pCubletPrefabs[0];
        _cubletIndexPrefabs[0, 1, 2] = pCubletPrefabs[2];
        _cubletIndexPrefabs[0, 2, 2] = pCubletPrefabs[3];
        _cubletIndexPrefabs[1, 0, 2] = pCubletPrefabs[4];
        _cubletIndexPrefabs[1, 1, 2] = pCubletPrefabs[2];
        _cubletIndexPrefabs[1, 2, 2] = pCubletPrefabs[4];
        _cubletIndexPrefabs[2, 0, 2] = pCubletPrefabs[4];
        _cubletIndexPrefabs[2, 1, 2] = pCubletPrefabs[2];
        _cubletIndexPrefabs[2, 2, 2] = pCubletPrefabs[0];

        // Middle Side
        _cubletIndexPrefabs[0, 0, 1] = pCubletPrefabs[2];
        _cubletIndexPrefabs[0, 1, 1] = pCubletPrefabs[2];
        _cubletIndexPrefabs[0, 2, 1] = pCubletPrefabs[2];
        _cubletIndexPrefabs[1, 0, 1] = pCubletPrefabs[1];
        _cubletIndexPrefabs[1, 1, 1] = pCubletPrefabs[1];
        _cubletIndexPrefabs[1, 2, 1] = pCubletPrefabs[2];
        _cubletIndexPrefabs[2, 0, 1] = pCubletPrefabs[2];
        _cubletIndexPrefabs[2, 1, 1] = pCubletPrefabs[1];
        _cubletIndexPrefabs[2, 2, 1] = pCubletPrefabs[2];

        // Front Side
        _cubletIndexPrefabs[0, 0, 0] = pCubletPrefabs[2];
        _cubletIndexPrefabs[0, 1, 0] = pCubletPrefabs[2];
        _cubletIndexPrefabs[0, 2, 0] = pCubletPrefabs[1];
        _cubletIndexPrefabs[1, 0, 0] = pCubletPrefabs[2];
        _cubletIndexPrefabs[1, 1, 0] = pCubletPrefabs[0];
        _cubletIndexPrefabs[1, 2, 0] = pCubletPrefabs[2];
        _cubletIndexPrefabs[2, 0, 0] = pCubletPrefabs[1];
        _cubletIndexPrefabs[2, 1, 0] = pCubletPrefabs[2];
        _cubletIndexPrefabs[2, 2, 0] = pCubletPrefabs[1];



        // Now go through all three dimensions of the cube
        for (int x = 0; x < Dimensions; x++)
        {
            for (int y = 0; y < Dimensions; y++)
            {
                for (int z = 0; z < Dimensions; z++)
                {
                    // Make sure we only create the elements that are on the outside
                    if (x == 0 || y == 0 || z == 0 || x == _border || y == _border || z == _border)
                    {
                        // Instantiate the cublet at the correct position and store it
                        GameObject cubletGO = Instantiate(_cubletIndexPrefabs[x, y, z], bottomLeftBack + new Vector3(x, y, z), Quaternion.identity);
                        Transform cubletTransform = cubletGO.transform;
                        _cublets[x, y, z] = cubletTransform;

                        // If our transform has 6 (colored) child gameObjects (one for every side),
                        // and pTurnInvisibleCubletSidesOff is true, process the visibility of all these sides,
                        // based on where the cublet is in the 3D array
                        if (pTurnInvisibleCubletSidesOff && cubletTransform.childCount == 6)
                        {
                            cubletTransform.GetChild(0).gameObject.SetActive(x == 0);
                            cubletTransform.GetChild(1).gameObject.SetActive(x == _border);
                            cubletTransform.GetChild(2).gameObject.SetActive(y == 0);
                            cubletTransform.GetChild(3).gameObject.SetActive(y == _border);
                            cubletTransform.GetChild(4).gameObject.SetActive(z == 0);
                            cubletTransform.GetChild(5).gameObject.SetActive(z == _border);
                        }

                        // Last but not least make sure each cublet is a child of our own transform
                        cubletTransform.SetParent(transform, false);
                    }
                }
            }
        }

        // Also create a backing store to help us when rotating a disc.
        _cubletBackingStore = new Transform[Dimensions, Dimensions];

        // Make sure we have a collider that fits, even though we don't use it ourselves,
        // if we don't create it here, all other scripts have to create their own
        GetComponent<BoxCollider>().size = Vector3.one * Dimensions;

        //IsDebugSolved();
    }

    /**
     * Rotates the given disc index of the cube over the given axis in the given direction.
     * 
     * pAxis has to be one of Vector3.right, forward or up (do not use the negative versions).
     * pLayer has to be between 0 (inc) and border (inc).
     * pPositive indicates a positive 90 degree rotation (true) over the given axis, or negative (false).
	 * Based on which side of the cube you are facing/viewing, a positive rotation might look like either a Clockwise or
	 * Counterclockwise rotation, and the same goes for a negative rotation. 
     * pRotateSpeed may override the default disc rotation speed
     * pRecord indicates whether to record these moves into the move history
     */
    public void RotateDisc(Vector3 pAxis, int pLayer, bool pPositiveRotation, float pRotateSpeed, bool pRecord = true)
    {
        if (IsDiscRotating)
        {
            return;
        }

        StartCoroutine(RotateDiscCoroutine(pAxis, pLayer, pPositiveRotation, pRotateSpeed, pRecord));
    }

    /**
     * Rotates the given disc index of the cube over the given axis in the given direction using a Coroutine.
     * 
     * pAxis has to be one of Vector3.right, forward or up
     * pLayer has to be between 0 (inc) and border (inc)
     * pPositive indicates a positive 90 degree rotation (true) over the given axis, or negative (false).
	 * Based on which side of the cube you are facing/viewing, a positive rotation might look like either a Clockwise or
	 * Counterclockwise rotation, and the same goes for a negative rotation. 
     * pRecord indicates whether to record these moves into the move history
     * pRotateSpeed may override the default disc rotation speed
     */
    public IEnumerator RotateDiscCoroutine(Vector3 pAxis, int pLayer, bool pPositiveRotation, float pRotateSpeed, bool pRecord = true)
    {
        if (IsDiscRotating)
        {
            yield break;
        }

        IsDiscRotating = true;

        //The passed in axis must be one of the cardinal axis
        Assert.IsTrue(Vector3.Dot(pAxis, Vector3.up) == 1 || Vector3.Dot(pAxis, Vector3.forward) == 1 || Vector3.Dot(pAxis, Vector3.right) == 1, "Axis assertion failed");
        Assert.IsTrue(pLayer >= 0 && pLayer <= _border, "Invalid layer passed");

        if (pRecord && _history.Length > 0)
        {
            RecordMove(new Move(pAxis, pLayer, pPositiveRotation));
        }

        // When we want to rotate a given disc we have to do two things:
        // - parent all the applicable cublets for that disc to our pivot using references from our 3D cublet array
        // - rotate the pivot 90 in the given direction over the given access
        // - unparent these cublets again
        // - update the 3d cublet array so that when we rotate another disc we are parenting the correct references to our pivot again

        // Which cublets we parent to our pivot depends on the provided axis and layer setting.
        //
        // For example:
        // - if the given axis is 1,0,0 and the given layer is 2, we parent all cublets from the disc with an x of 2.
        // - if the given axis is 0,1,0 and the given layer is 4 (assuming a 4*4*4 cube), we parent all cublets from the disc with a y of 4.
        // - if the given axis is 0,0,1 and the given layer is 0, we parent all cublets from the disc with a z of 0.

        // In order to 'gather' or loop over the cublets in the disc that we want to rotate,
        // we need not only the rotation axis and layer,
        // but also the two axis (let's call them uAxis and vAxis) that are perpendicular to the given axis.
        // Using these vectors we can create a 2d (eg nested) for loops to iterate over all cublets in the disc
        // specified by the given axis and layer.

        Vector3 uAxis = Vector3.zero;
        Vector3 vAxis = Vector3.zero;

        //we make sure we always stick to the order x,y,z,x,y,z while enumerating these axis

        if (pAxis.x == 1)				//x.. y,z
        {
            uAxis = Vector3.up;			//y
            vAxis = Vector3.forward;	//z
        }
        else if (pAxis.y == 1)			//y.. z,x
        {
            uAxis = Vector3.forward;	//z
            vAxis = Vector3.right;		//x
        }
        else if (pAxis.z == 1)			//z.., x, y
        {
            uAxis = Vector3.right;		//x
            vAxis = Vector3.up;			//y
        }

        //using these axis we can iterate over all cublets in a specific layer/disc so we can
        //- parent them to our pivot for the visual rotation effect
        //- rotate their indices in the underlying 3d array within the current disc so that this method
        //  can be called more than once without breaking ;)

        //In order to rotate the elements in their current slice we need a (float) center point
        //It has to be a float since the rubik cube's dimensions may be even or odd
        float centerIndex = _border / 2.0f;
        Vector2 center = new(centerIndex, centerIndex);

        //Now iterate over the whole 2d disc within our 3d array, using u and v iterators (instead of x and y),
        //and the derived uAxis and vAxis. The values of u and v are always from 0 to _dimensions-1.

        for (int u = 0; u < Dimensions; u++)
        {
            for (int v = 0; v < Dimensions; v++)
            {
                // Make sure we only process elements for which we actually have a cublet
                if (u == 0 || v == 0 || u == _border || v == _border || pLayer == 0 || pLayer == _border)
                {
                    // Get the correct cublet based on the rotation axis and u and v directions and parent it to our pivot
                    Vector3 cubletIndex = pAxis * pLayer + uAxis * u + vAxis * v;
                    Transform cublet = _cublets[(int)cubletIndex.x, (int)cubletIndex.y, (int)cubletIndex.z];
                    cublet.parent = _pivot;

                    // Also directly copy it to a rotated position in our backing store
                    // (we could also copy it without rotating, then rotate it and then copy it back,
                    // (or rotate it while copying it back, but I found this easier)
                    // We want to rotate the current uv coordinates around the center of the disc,
                    // which means subtract the center, rotate the coords, and add the center again
                    // (and yes you could also collapse this into a 1 liner, but understanding it is also good)
                    Vector2 originalUV = new(u, v);
                    Vector2 translatedUV = originalUV - center;

                    // A positive rotation of 90 degrees in 2D is the same as doing this:
                    Vector2 rotatedUV = new(-translatedUV.y, translatedUV.x);

                    // Or negative it if needed
                    if (!pPositiveRotation)
                    {
                        rotatedUV *= -1;
                    }
                    //don't forget to add the center back in
                    rotatedUV += center;

                    //now store the cublet at its rotated position in the backing store
                    _cubletBackingStore[(int)rotatedUV.x, (int)rotatedUV.y] = cublet;
                }
            }
        }

        // Last step is to copy everything from the backing store back into the 3d cublet array and we've
        // rotated the correct slice in our 3d array.
        for (int u = 0; u < Dimensions; u++)
        {
            for (int v = 0; v < Dimensions; v++)
            {
                Vector3 cubletIndex = pAxis * pLayer + uAxis * u + vAxis * v;

                if (u == 0 || v == 0 || u == _border || v == _border || pLayer == 0 || pLayer == _border)
                {
                    _cublets[(int)cubletIndex.x, (int)cubletIndex.y, (int)cubletIndex.z] = _cubletBackingStore[u, v];
                }
            }
        }

        // Now do the visual part
        yield return StartCoroutine(RotatePivotCoroutine(pAxis, pPositiveRotation, pRotateSpeed));
    }

    /**
     * Performs the visual rotation of the pivot/disc around the given axis etc
     */
    private IEnumerator RotatePivotCoroutine(Vector3 pAxis, bool pPositiveRotation, float pRotateSpeed)
    {
        //store the current rotation so we can reset and do a clean 90 degree rotation after the animation
        Quaternion currentRotation = _pivot.localRotation;

        //calculate the rotation speed and direction
        float angle = pPositiveRotation ? 90 : -90;

        //start a while loop to rotate at least 90 degrees        
        Quaternion targetRotation = Quaternion.Euler(pAxis * angle);

        float rotated = 0;
        while (rotated < 1)
        {
            rotated += Time.deltaTime * pRotateSpeed;
            _pivot.localRotation = Quaternion.Lerp(currentRotation, targetRotation, rotated);
            yield return null;
        }

        //Reset the rotation and do a clean 90 angle to avoid drifting/overshooting
        _pivot.localRotation = currentRotation;
        _pivot.Rotate(pAxis, angle, Space.Self);

        //Unparent all cublet from pivot and to us again
        int pivotChildCount = _pivot.childCount;
        Transform child = null;
        while (pivotChildCount > 0)
        {
            child = _pivot.GetChild(--pivotChildCount);
            child.parent = transform;
            //counter any drifting in scale
            child.localScale = Vector3.one;
        }

        //reset pivot for next go, not entirely necessary but I find it conceptually easier, 
        //might need to change that somewhere in the future
        _pivot.localRotation = Quaternion.identity;
        IsDiscRotating = false;

        //dispatch any events if required
        OnChanged?.Invoke();
        //if (OnSolved != null && IsDebugSolved())
        //{
        //    OnSolved.Invoke();
        //}
    }

    /**
     * Shuffle the cube with your own values.
     */
    public void Shuffle(List<Path> paths, float shuffleSpeed)
    {
        if (IsDiscRotating)
        {
            return;
        }

        StartCoroutine(ShuffleCoroutine(paths, shuffleSpeed));
    }

    /**
     * Shuffle the cube with your own values using a coroutine.
     */
    public IEnumerator ShuffleCoroutine(List<Path> paths, float shuffleSpeed)
    {
        if (IsDiscRotating)
        {
            yield break;
        }

        int lastAxisIndex = Random.Range(0, 3);

        foreach (Path path in paths)
        {
            yield return RotateDiscCoroutine(
                path.Axis,                       //random axis
                path.Layer,                //random layer
                path.PositiveRotation,                        //random direction
                shuffleSpeed,                      //rotation speed
                false                                       //do not store in history
            );
        }
    }

    /**
      * Records the given move so we can undo it later
      */
    private void RecordMove(Move pMove)
    {
        //store this move in the history so we can undo it (we use a circular history buffer to avoid copying a lot)
        _history[_currentHistoryIndex] = pMove;

        _currentHistoryIndex++;
        if (_currentHistoryIndex == _history.Length)
        {
            _currentHistoryIndex = 0;
        }

        _currentHistorySize = Mathf.Min(_currentHistorySize + 1, _history.Length);
    }

    /**
     * Undoes the last move.
     */
    public void Undo(float pUndoSpeed)
    {
        if (IsDiscRotating) return;

        if (_currentHistorySize > 0)
        {
            _currentHistoryIndex--;
            if (_currentHistoryIndex == -1) _currentHistoryIndex = _history.Length - 1;

            Move move = _history[_currentHistoryIndex];
            RotateDisc(move.axis, move.layer, !move.positive, pUndoSpeed, false);

            _currentHistorySize = Mathf.Max(_currentHistorySize - 1, 0);
        }
    }

    /**
     * Check if the cube is solved: the cube is solved if all cublets are aligned in the same orientation... WRONG.
     * On further inspection this is unfortunately not true, we need to inspect each side individually since the 
     * center of each side might be rotated within its own side. 
     * 
     * Left this in here for posterity as a demo of how not to do it.
     *
    bool isSolved()
    {
        //just pick any element
        Quaternion initialRotation = _cublets[0, 0, 0].transform.localRotation;

        for (int x = 0; x < dimensions; x++)
        {
            for (int y = 0; y < dimensions; y++)
            {
                for (int z = 0; z < dimensions; z++)
                {
                    if (x == 0 || y == 0 || z == 0 || x == _border || y == _border || z == _border)
                    {
                        //the moment you hit a cublet that is aligned differently return false
                        //we could also check if the dot is 1, but floating point errors etc...
                        if (Quaternion.Dot(_cublets[x, y, z].transform.localRotation, initialRotation) < 0.999f) return false;
                    }
                }
            }
        }
        return true;
    }
    */
}

public enum Side
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right
}

//records a move for undo
class Move
{
    public readonly Vector3 axis;
    public readonly int layer;
    public readonly bool positive;

    public Move(Vector3 pAxis, int pLayer, bool pPositive)
    {
        axis = pAxis;
        layer = pLayer;
        positive = pPositive;
    }
}