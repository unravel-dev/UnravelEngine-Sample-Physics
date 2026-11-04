using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// A button for a joint's motor. Pressed, it turns the motor the other way (a lift driven against its limits goes
    /// to the other end and stays there) or switches it on and off (a windmill). The station's reset sets it back.
    /// </summary>
    public class MotorSwitch : StationPart
    {
        public enum Action
        {
            Reverse,
            Toggle,
        }

        [Tooltip("The entity with the JointComponent whose motor this drives.")]
        public Entity joint;

        public Action action = Action.Reverse;

        [Tooltip("Text that shows the motor's state, optional.")]
        public Entity label;

        [Tooltip("Label text while the motor turns forward (or runs); {0} is its speed.")]
        public string forwardText = "Going up";

        [Tooltip("Label text while the motor turns back (or stands).")]
        public string backText = "Going down";

        private JointMotor startMotor;
        private JointComponent motorJoint;

        public override void OnStart()
        {
            motorJoint = joint ? joint.GetComponent<JointComponent>() : null;
            if (motorJoint != null)
            {
                startMotor = motorJoint.motor;
            }
            var button = owner.GetComponent<Interactable>();
            if (button != null)
            {
                button.pressed += _ => Press();
            }
            ShowState();
        }

        /// <summary>Reverses or toggles the motor.</summary>
        public void Press()
        {
            if (motorJoint == null)
            {
                return;
            }
            var motor = motorJoint.motor;
            if (action == Action.Reverse)
            {
                motor.enabled = true;
                motor.speed = -motor.speed;
            }
            else
            {
                motor.enabled = !motor.enabled;
            }
            motorJoint.motor = motor;
            if (motorJoint.bodyB)
            {
                motorJoint.bodyB.GetComponent<PhysicsComponent>()?.WakeUp();
            }
            ShowState();
        }

        public override void ResetPart()
        {
            if (motorJoint != null)
            {
                motorJoint.motor = startMotor;
            }
            ShowState();
        }

        private void ShowState()
        {
            var text = label ? label.GetComponent<TextComponent>() : null;
            if (text == null || motorJoint == null)
            {
                return;
            }
            var motor = motorJoint.motor;
            bool forward = action == Action.Reverse ? motor.speed >= 0.0f : motor.enabled;
            text.text = string.Format(forward ? forwardText : backText, Mathf.Abs(motor.speed));
        }
    }
}
