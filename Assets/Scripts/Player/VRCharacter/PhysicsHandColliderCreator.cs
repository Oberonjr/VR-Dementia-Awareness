using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Dynamically generates capsule colliders on hand bone joints to enable physics interaction without independent rigidbodies
/// </summary>
public class PhysicsHandColliderCreator : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private HandVisual handVisual;

    [Header("Collider Settings")]
    [SerializeField] private float fingerRadius = 0.012f;
    [SerializeField] private PhysicsMaterial physicsMaterial;

    private bool collidersGenerated;

    private void Update()
    {
        // Wait until the HandVisual bones have been fully loaded
        if (!collidersGenerated && handVisual != null && handVisual.Joints.Count > 0 && handVisual.Joints[0] != null)
        {
            GenerateFingerColliders();
            collidersGenerated = true;
        }
    }

    private void GenerateFingerColliders()
    {
        for (int i = (int)HandJointId.HandThumb1; i < (int)HandJointId.HandEnd; ++i)
        {
            HandJointId currentJoint = (HandJointId)i;
            HandJointId parentJoint = HandJointUtils.JointParentList[i];

            if (parentJoint == HandJointId.Invalid) { continue; }

            Transform currentTransform = handVisual.GetTransformByHandJointId(currentJoint);
            Transform parentTransform = handVisual.GetTransformByHandJointId(parentJoint);

            if (currentTransform == null || parentTransform == null) { continue; }

            CreateCapsuleForBone(parentJoint.ToString(), parentTransform, currentTransform.position);
        }
    }

    private void CreateCapsuleForBone(string boneName, Transform parentTransform, Vector3 targetPosition)
    {
        GameObject capsuleObj = new GameObject($"{boneName}_PhysicsCapsule");
        capsuleObj.layer = LayerMask.NameToLayer("Physics Hands");
        capsuleObj.transform.SetParent(parentTransform, false);

        CapsuleCollider capsule = capsuleObj.AddComponent<CapsuleCollider>();
        if (physicsMaterial != null) { capsule.sharedMaterial = physicsMaterial; }

        Vector3 direction = targetPosition - parentTransform.position;
        float distance = direction.magnitude;

        capsuleObj.transform.position = parentTransform.position + (direction * 0.5f);
        capsuleObj.transform.rotation = Quaternion.LookRotation(direction);

        // Setting direction to 2 aligns the capsule collider horizontally along the Z-axis
        capsule.direction = 2;
        capsule.radius = fingerRadius;
        capsule.height = distance + (capsule.radius * 2.0f);
    }
}