


using UnityEngine;
using UnityEngine.InputSystem;

// Note: Animations called via CharacterController for both character and capsule using animator null checks.
// Note: Grounded is set by detecting collision on-the-fly against "ground" layers.

namespace GameDev {

	[RequireComponent(typeof(CharacterController))]
	[RequireComponent(typeof(PlayerInput))]

	public class CharacterControllerFirstPerson : MonoBehaviour {

		[Header("Movement")]
		[Tooltip("Movement speed of the character in m/s.")]
		public float MoveSpeed = 5.0f;
		[Tooltip("Sprinting speed of the character in m/s.")]
		public float SprintSpeed = 10.0f;
		[Tooltip("Rotation speed of the character in deg/s.")]
		public float RotateSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float Acceleration = 10.0f; // SpeedChangeRate
		[Tooltip("The height the player can jump (meters).")]
		public float JumpHeight = 1.0f;
		[Tooltip("The character uses its own gravity value (meters/secs^2).")]
		public float CharacterGravity = -15.0f;
		[Tooltip("Time (secs) required to pass after jumping before being able to jump again. If 0 the character can instantly jump again.")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time (secs) required to pass before entering the fall state. Useful for walking down stairs.")]
		public float FallTimeout = 0.1f;
		[Header("Grounded")]
		[Tooltip("If character is grounded or not.")]
		public bool Grounded = true;
		[Tooltip("Offset (meters) useful for rough grounds.")]
		public float GroundedOffset = -0.1f;
		[Tooltip("Radius (meters) of grounded check (should match radius of CharacterController).")]
		public float GroundedRadius = 0.5f;
		[Tooltip("Layers character uses as ground.")]
		public LayerMask GroundedLayers;
		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("THe max angle (deg) you can tilt the camera up.")]
		public float CameraAngleClampTop = 90.0f;
		[Tooltip("The max angle (deg) you can tilt the camera down.")]
		public float CameraAngleClampBottom = -90.0f;
		[Header("Pushing")]
		[Tooltip("If character can push non-kinematic rigidbodies.")]
		public bool PushingEnabled = true;
		[Tooltip("Layers character can push.")]
		public LayerMask PushingLayers;
		[Tooltip("Pushing strength (Newtons).")]
		[Range(0.5f, 5f)] public float strength = 1.1f;
		
		// Inputs.
		private Vector2 inputMoveValue;
		private Vector2 inputLookValue;
		private bool inputJumpValue;
		private bool inputSprintValue;
		private bool inputAnalogMovement;
		private bool inputMouseCursorLocked = false; //true; // TURINIG
		private bool inputMouseCursorInputForLook = true;
		private float inputThreshold = 0.01f;

		// Cinemachine.
		private float cinemachineTargetPitch;

		// Player.
		private float velocity;
		private float velocityRotate;
		private float velocityVertical;
		private float velocityTerminal = 53.0f;

		// Timeout deltatimes.
		private float timerJumpTimeout;
		private float timerFallTimeout;
		
		// ...
		private CharacterController characterController;

		// Event function for the Move action.
		public void OnMove(InputValue value) { 
			this.inputMoveValue = value.Get<Vector2>();
		}

		// Event function for the Look action.
		public void OnLook(InputValue value) {
			if(this.inputMouseCursorInputForLook) {
				this.inputLookValue = value.Get<Vector2>(); }
		}

		// Event function for the Jump action.
		public void OnJump(InputValue value) {
			this.inputJumpValue = value.isPressed;
		}

		// Event function for the Sprint action.
		public void OnSprint(InputValue value) {
			this.inputSprintValue = value.isPressed;
		}

		// Event function...
		void OnControllerColliderHit(ControllerColliderHit hit)
		{
			if (this.PushingEnabled) { PushRigidBodies(hit); }
		}

		// Event function...
		void OnApplicationFocus(bool hasFocus) {
			if (this.inputMouseCursorLocked) { Cursor.lockState = CursorLockMode.Locked; }
			else { Cursor.lockState = CursorLockMode.None; }
		}

		// Event function...
		void Start() {
			// ...
			this.characterController = GetComponent<CharacterController>();
			// Reset our timeouts on start
			this.timerJumpTimeout = JumpTimeout;
			this.timerFallTimeout = FallTimeout;
		}

		// Event function...
		void Update() {
			JumpAndApplyGravity();
			CheckGrounded();
			MoveCharacter();
		}

		// Event function...
		void LateUpdate() {
			RotateCamera();
		}

		// Internal function to push rigidbodies.
		private void PushRigidBodies(ControllerColliderHit hit)
		{
			// Check if character hit a non-kinematic rigidbody.
			Rigidbody hitRigidbody = hit.collider.attachedRigidbody;
			if ((hitRigidbody == null) || (hitRigidbody.isKinematic)) { return; }
			// Check if hit rigidbody is on a "pushable" layer.
			int hitRigidbodyLayer = 1 << hitRigidbody.gameObject.layer;
			if ((hitRigidbodyLayer & this.PushingLayers.value) == 0) { return; }
			// Ensure pushing direction is mostly on XZ plane (no pushing on Y axis).
			if (hit.moveDirection.y < -0.3f) { return; }
			// Calculate pushing direction from move direction (only on XZ plane, no pushing on Y axis).
			Vector3 pushingDirection = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);
			// Apply pushing force (impulse) considering custom pushing strength.
			hitRigidbody.AddForce(pushingDirection * strength, ForceMode.Impulse);
		}

		// Internal function to check if character is grounded.
		private void CheckGrounded() {
			// Set sphere position (with ground offset) and check if touches the ground.
			Vector3 spherePosition = new Vector3( this.transform.position.x, 
												  this.transform.position.y - this.GroundedOffset, 
												  this.transform.position.z );
			this.Grounded = Physics.CheckSphere( spherePosition, 
				                                 this.GroundedRadius, 
												 this.GroundedLayers, 
												 QueryTriggerInteraction.Ignore );
		}

		// Internal function to rotate the camera.
		private void RotateCamera() {
			// Check if there is "enough" input.
			if( this.inputLookValue.sqrMagnitude >= this.inputThreshold ) {
				// ...
				this.cinemachineTargetPitch += this.inputLookValue.y * this.RotateSpeed * Time.deltaTime;
				this.velocityRotate = this.inputLookValue.x * this.RotateSpeed * Time.deltaTime;
				// Clamp pitch rotation.
				this.cinemachineTargetPitch = ClampAngle( this.cinemachineTargetPitch, 
					                                      this.CameraAngleClampBottom, 
														  this.CameraAngleClampTop );
				// Update Cinemachine camera target pitch.
				this.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler( this.cinemachineTargetPitch, 0.0f, 0.0f );
				// Rotate player left-right.
				this.transform.Rotate( Vector3.up * this.velocityRotate);
			}
		}

		// Internal function to move the character.
		private void MoveCharacter() {
			// Set target velocity based on move speed, sprint speed and if sprint is pressed.
			float targetVelocity = this.MoveSpeed;
			if ( this.inputSprintValue ) { targetVelocity = this.SprintSpeed; }
			// Simple acceleration/deceleration.
			// Note: Equality operator (==) in Vector2 uses approximation (not floating point error prone), and is cheaper than magnitude.
			// Check if there is no input.
			if (this.inputMoveValue == Vector2.zero) { targetVelocity = 0.0f; }
			// The player current horizontal velocity.
			float currHorizontalVelocity = new Vector3( this.characterController.velocity.x, 
				                                        0.0f, 
														this.characterController.velocity.z ).magnitude;
			// ...
			float offsetVelocity = 0.1f;
			float inputMagnitude = 1.0f;
			if (this.inputAnalogMovement) { inputMagnitude = this.inputMoveValue.magnitude; }
			// Check acceleration/deceleration to target speed.
			if ((currHorizontalVelocity < targetVelocity - offsetVelocity) || 
				(currHorizontalVelocity > targetVelocity + offsetVelocity)) {
				// Use LERP to provide smoother speed change
				// Note: T (last arg) in Lerp is already clamped.
				this.velocity = Mathf.Lerp( currHorizontalVelocity, 
					                        targetVelocity * inputMagnitude, 
											Time.deltaTime * this.Acceleration );
				// Round velocity.
				this.velocity = Mathf.Round( this.velocity * 1000.0f ) / 1000.0f;
			}
			else { this.velocity = targetVelocity; }
			// Normalize input direction.
			Vector3 inputDirection = new Vector3(this.inputMoveValue.x, 0.0f, this.inputMoveValue.y).normalized;
			// Note: Difference operation (!=) in Vector2 uses approximation (not floating point error prone), and is cheaper than magnitude.
			// Check if there is no input.
			if (this.inputMoveValue != Vector2.zero) {
				// Calculate move.
				inputDirection = this.transform.right * this.inputMoveValue.x + this.transform.forward * this.inputMoveValue.y;
			}
			// Apply move to character controller.
			this.characterController.Move( inputDirection.normalized * (this.velocity * Time.deltaTime) + 
				                           new Vector3(0.0f, this.velocityVertical, 0.0f) * Time.deltaTime );
		}

		// Internal function to jump and apply gravity.
		private void JumpAndApplyGravity() {
			// Check if controller is grounded.
			if (this.Grounded) {
				// Reset fall timeout timer.
				this.timerFallTimeout = this.FallTimeout;
				// Stop velocity dropping infinitely when grounded.
				if (this.velocityVertical < 0.0f) { this.velocityVertical = -2.0f; }
				// Compute jump.
				if ((this.inputJumpValue) && (this.timerJumpTimeout <= 0.0f)) {
					// sqrt(H * -2 * G) == velocity needed to reach desired H.
					this.velocityVertical = Mathf.Sqrt( this.JumpHeight * -2.0f * this.CharacterGravity );
				}
				// Update jump timeout timer.
				if (this.timerJumpTimeout >= 0.0f) { this.timerJumpTimeout -= Time.deltaTime; }
			}
			else {
				// Reset the jump timeout timer.
				this.timerJumpTimeout = this.JumpTimeout;
				// Update fall timeout timer.
				if (this.timerFallTimeout >= 0.0f) { this.timerFallTimeout -= Time.deltaTime; }
				// Force disable jump if not grounded.
				this.inputJumpValue = false;
			}
			// Apply gravity over time if under terminal velocity (multiply by delta time twice to linearly speed up over time)
			if (this.velocityVertical < this.velocityTerminal) { this.velocityVertical += this.CharacterGravity * Time.deltaTime; }
		}

		// Internal function to clamp input angle (also ensuring input angle is in (-360, 360)).
		private static float ClampAngle(float angle, float min, float max) {
			if (angle < -360.0f) { angle += 360.0f; }
			if (angle > 360.0f) { angle -= 360.0f; }
			return Mathf.Clamp(angle, min, max);
		}

		// Event function...
		void OnDrawGizmosSelected() {
			// ...
			Color transpGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transpRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);
			// ...
			if (this.Grounded) { Gizmos.color = transpGreen; }
			else { Gizmos.color = transpRed; }
			// Draw a sphere gizmo using position/radius of grounded collider.
			Gizmos.DrawSphere( new Vector3( this.transform.position.x, 
				                            this.transform.position.y - this.GroundedOffset, 
											this.transform.position.z ), 
							   this.GroundedRadius );
		}

	}

}


