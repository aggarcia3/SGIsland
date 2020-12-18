using UnityEngine;

namespace SGIsland.Rendering
{
    /// <summary>
    /// Corrects the rotation of the star particle system so that it looks like
    /// a infinitely distant star field that rotates with the view.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class HorizonStarParallaxCorrector : MonoBehaviour
    {
        private void LateUpdate()
        {
            Transform parentTransform;
            if (parentTransform = transform.parent)
            {
                // We want to set the world rotation of this game object so that its
                // world rotation is the same as the local rotation of the parent.
                // This way stars rotate around with the parent transform, which is
                // what we want
                Vector3 parentRotation = parentTransform.localRotation.eulerAngles;
                parentRotation[0] = -90;
                parentRotation[1] = -parentRotation.y;
                parentRotation[2] = -parentRotation.z;

                transform.rotation = Quaternion.Euler(parentRotation);
            }
        }
    }
}
