using UnityEngine;

namespace Scripts.Rendering
{
    /// <summary>
    /// Keeps the world overlay camera transform synchronized with main camera's.
    /// </summary>
    public class SynchronizeWorldOverlayCamera : MonoBehaviour
    {
        [SerializeField]
        private string mainCameraTag;

        private GameObject mainCamera;

        private void Start()
        {
            mainCamera = GameObject.FindGameObjectWithTag(mainCameraTag);
        }

        private void Update()
        {
            gameObject.transform.position = mainCamera.transform.position;
            gameObject.transform.rotation = mainCamera.transform.rotation;
        }
    }
}
