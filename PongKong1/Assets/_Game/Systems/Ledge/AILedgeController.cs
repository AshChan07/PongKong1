using UnityEngine;
using PongKong.Systems.AI; // Needed to access the AIAction struct

namespace PongKong.Systems.Ledge
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class AILedgeController : MonoBehaviour
    {
        [Header("Settings")]
        public float movementSpeed = 8f; // The dev plan specifies 8 units/sec base speed
        
        // Event Channel reference:
        // public AIActionEventChannel onAIDecision;

        private float targetY;
        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            targetY = transform.position.y;
        }

        private void Start()
        {
            // Subscribe to the AI decision event
            // if (onAIDecision != null) onAIDecision.OnEventRaised += HandleAIDecision;
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            // if (onAIDecision != null) onAIDecision.OnEventRaised -= HandleAIDecision;
        }

        /// <summary>
        /// This method receives the target from the AISystem.
        /// </summary>
        public void HandleAIDecision(AIAction action)
        {
            targetY = action.MoveTarget.y;
        }

        private void FixedUpdate()
        {
            // Smoothly move the ledge's Y position towards the AI's requested target Y.
            // Using Rigidbody2D.MovePosition ensures proper collision with the ball and walls.
            Vector2 currentPos = rb.position;
            currentPos.y = Mathf.MoveTowards(currentPos.y, targetY, movementSpeed * Time.fixedDeltaTime);
            
            rb.MovePosition(currentPos);
        }
    }
}
