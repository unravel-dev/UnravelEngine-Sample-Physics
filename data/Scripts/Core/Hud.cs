using System.Collections.Generic;
using Unravel.Core;

namespace PhysicsSample
{
    /// <summary>
    /// The data model "hud" of UI/Hud.rhtml. Station rows and the crosshair's state are plain fields the document
    /// binds to; the buttons call the methods.
    /// </summary>
    public sealed class HudModel
    {
        public string title = "";
        public string description = "";
        public string controls = "";
        public bool hasStation;
        /// <summary>What the crosshair is on: "", "body", "heavy", "ragdoll", "use" or "carry".</summary>
        public string aimKind = "";
        public string aimText = "";
        public bool helpOpen;
        public bool menuOpen;
        public bool cursorFree;
        public bool driving;
        public List<StationRow> stations = new List<StationRow>();
        public string gravityText = "";

        internal System.Action<int> goTo;
        internal System.Action reset;
        internal System.Action closeAll;

        public void GoTo(int index)
        {
            goTo?.Invoke(index);
        }

        public void ResetStation()
        {
            reset?.Invoke();
        }

        public void Close()
        {
            closeAll?.Invoke();
        }
    }

    /// <summary>A row of the station menu.</summary>
    public sealed class StationRow
    {
        public int index;
        public string title = "";
        public string summary = "";
        public bool current;
    }

    /// <summary>
    /// Drives the HUD: the crosshair says what the physics tool would do, the card at the bottom left describes the
    /// station the visitor stands at, Tab opens the station menu (teleports), F1 the controls, R resets the station.
    /// </summary>
    public class Hud : ScriptComponent
    {
        [Tooltip("The entity with the HUD's UI document.")]
        public Entity document;

        [Tooltip("The player, with the FirstPersonController.")]
        public Entity player;

        [Tooltip("The camera with the PhysicsTool.")]
        public Entity tool;

        public AudioClip clickSound;

        /// <summary>Whether a panel takes the mouse, so a click there does not lock the cursor.</summary>
        public static bool isPointerOverPanel { get; private set; }

        private readonly HudModel hud = new HudModel();
        private UIDataModel model;
        private List<Station> stations = new List<Station>();
        private Station current;
        private FirstPersonController walker;
        private PhysicsTool physicsTool;
        private string lastAim = "";

        public override void OnStart()
        {
            walker = player.GetComponent<FirstPersonController>();
            physicsTool = tool.GetComponent<PhysicsTool>();
            stations = Station.All();
            for (int i = 0; i < stations.Count; ++i)
            {
                hud.stations.Add(new StationRow { index = i, title = stations[i].title, summary = stations[i].description });
            }
            hud.goTo = GoTo;
            hud.reset = ResetCurrent;
            hud.closeAll = CloseAll;
            model = document.GetComponent<UIDocumentComponent>().GetModel("hud");
            model.Bind(hud);
        }

        public override void OnUpdate()
        {
            UpdateKeys();
            UpdateStation();
            UpdateAim();
            hud.cursorFree = Cursor.lockState != CursorLockMode.Locked;
            isPointerOverPanel = hud.menuOpen || hud.helpOpen;
            model.Dirty("cursorFree");
            if (hud.gravityText != GravitySwitch.description)
            {
                hud.gravityText = GravitySwitch.description;
                model.Dirty("gravityText");
            }
        }

        /// <summary>Shown while the visitor drives the buggy: the crosshair hides and the card says how to drive.</summary>
        public void SetDriving(bool driving)
        {
            hud.driving = driving;
            model.Dirty("driving");
        }

        private void UpdateKeys()
        {
            if (Input.IsPressed(KeyCode.F1))
            {
                hud.helpOpen = !hud.helpOpen;
                hud.menuOpen = false;
                SetCursorFree(hud.helpOpen);
                model.Dirty("helpOpen");
                model.Dirty("menuOpen");
            }
            if (Input.IsPressed(KeyCode.Tab))
            {
                hud.menuOpen = !hud.menuOpen;
                hud.helpOpen = false;
                SetCursorFree(hud.menuOpen);
                model.Dirty("helpOpen");
                model.Dirty("menuOpen");
            }
            if (Input.IsPressed(KeyCode.R) && current != null && !hud.driving)
            {
                ResetCurrent();
            }
            for (int i = 0; i < stations.Count && i < 9; ++i)
            {
                if (Input.IsPressed(KeyCode.Digit1 + i))
                {
                    GoTo(i);
                }
            }
        }

        private static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        private void UpdateStation()
        {
            Station nearest = null;
            float best = float.MaxValue;
            Vector3 position = player.transform.position;
            foreach (var station in stations)
            {
                float distance = Vector3.Distance(position, station.transform.position);
                if (distance < station.radius && distance < best)
                {
                    best = distance;
                    nearest = station;
                }
            }
            if (nearest == current)
            {
                return;
            }
            current = nearest;
            hud.hasStation = current != null;
            hud.title = current != null ? current.title : "";
            hud.description = current != null ? current.description : "";
            hud.controls = current != null ? current.controls : "";
            for (int i = 0; i < hud.stations.Count; ++i)
            {
                hud.stations[i].current = stations[i] == current;
            }
            model.DirtyAll();
        }

        private void UpdateAim()
        {
            string kind = "";
            string text = "";
            if (physicsTool.carriedBody)
            {
                kind = "carry";
                text = "Click to throw, release to drop, wheel to move it";
            }
            else if (physicsTool.aim.HasValue)
            {
                var hit = physicsTool.aim.Value;
                var interactable = Interactable.Find(hit.entity);
                var body = hit.entity.GetComponent<PhysicsComponent>();
                if (interactable != null)
                {
                    kind = "use";
                    text = interactable.prompt;
                }
                else if (RagdollCharacter.Find(hit.entity) != null)
                {
                    kind = "ragdoll";
                    text = "Click to push";
                }
                else if (body != null && body.bodyType == RigidbodyType.Dynamic)
                {
                    bool heavy = body.mass > physicsTool.maxCarryMass;
                    kind = heavy ? "heavy" : "body";
                    text = heavy ? string.Format("{0:0} kg - too heavy to carry", body.mass)
                                 : string.Format("{0:0.#} kg - click to push, hold right to carry", body.mass);
                }
            }
            string key = kind + text;
            if (key == lastAim)
            {
                return;
            }
            lastAim = key;
            hud.aimKind = kind;
            hud.aimText = text;
            model.Dirty("aimKind");
            model.Dirty("aimText");
        }

        private void GoTo(int index)
        {
            if (index < 0 || index >= stations.Count || hud.driving)
            {
                return;
            }
            var station = stations[index];
            Entity view = station.viewpoint ? station.viewpoint : station.owner;
            walker.TeleportTo(view.transform.position, Heading.Of(view));
            CloseAll();
            Click();
        }

        private void ResetCurrent()
        {
            current?.ResetStation();
            Click();
        }

        private void CloseAll()
        {
            hud.menuOpen = false;
            hud.helpOpen = false;
            SetCursorFree(false);
            model.Dirty("menuOpen");
            model.Dirty("helpOpen");
        }

        private void Click()
        {
            var source = owner.GetComponent<AudioSourceComponent>();
            if (source != null && clickSound != null)
            {
                source.clip = clickSound;
                source.Play();
            }
        }
    }
}
