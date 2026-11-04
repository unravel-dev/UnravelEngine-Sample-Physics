using System.Collections.Generic;
using Unravel.Core;
using Unravel.Samples;

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
        /// <summary>The station card shows its title alone: on a touch screen it folds a while after it opens.</summary>
        public bool cardFolded;
        /// <summary>What the crosshair is on: "", "body", "heavy", "ragdoll", "use" or "carry".</summary>
        public string aimKind = "";
        public string aimText = "";
        public bool helpOpen;
        public bool menuOpen;
        public bool cursorFree;
        public bool driving;
        /// <summary>A touch screen: the texts name the on-screen buttons, and the menus open from buttons of their own.</summary>
        public bool touch;
        public List<StationRow> stations = new List<StationRow>();
        public string gravityText = "";

        internal System.Action<int> goTo;
        internal System.Action reset;
        internal System.Action closeAll;
        internal System.Action toggleMenu;
        internal System.Action toggleHelp;
        internal System.Action toggleCard;

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

        public void ToggleMenu()
        {
            toggleMenu?.Invoke();
        }

        public void ToggleHelp()
        {
            toggleHelp?.Invoke();
        }

        public void ToggleCard()
        {
            toggleCard?.Invoke();
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
    /// On a touch screen buttons at the top open the menus, the card moves to the top, and the on-screen buttons are
    /// named for what they do at the moment: Push throws while the tool carries, and the buggy brakes with Jump.
    /// </summary>
    public class Hud : ScriptComponent
    {
        [Tooltip("The entity with the HUD's UI document.")]
        public Entity document;

        [Tooltip("The player, with the FirstPersonController.")]
        public Entity player;

        [Tooltip("The camera with the PhysicsTool.")]
        public Entity tool;

        [Tooltip("The entity with the on-screen controls (SampleTouchJoystick).")]
        public Entity touchControls;

        [Tooltip("Seconds the station card shows in full on a touch screen before it folds to its title; a tap on it " +
                 "opens it again.")]
        public float cardSeconds = 8.0f;

        public AudioClip clickSound;

        /// <summary>Whether a panel takes the mouse, so a click there does not lock the cursor.</summary>
        public static bool isPointerOverPanel { get; private set; }

        private readonly HudModel hud = new HudModel();
        private UIDataModel model;
        private List<Station> stations = new List<Station>();
        private Station current;
        private FirstPersonController walker;
        private PhysicsTool physicsTool;
        private SampleTouchJoystick touchJoystick;
        private float cardOpenedAt;
        private string lastAim = "";

        public override void OnStart()
        {
            walker = player.GetComponent<FirstPersonController>();
            physicsTool = tool.GetComponent<PhysicsTool>();
            touchJoystick = touchControls ? touchControls.GetComponent<SampleTouchJoystick>() : null;
            stations = Station.All();
            for (int i = 0; i < stations.Count; ++i)
            {
                hud.stations.Add(new StationRow { index = i, title = stations[i].title, summary = stations[i].description });
            }
            hud.goTo = GoTo;
            hud.reset = ResetCurrent;
            hud.closeAll = CloseAll;
            hud.toggleMenu = ToggleMenu;
            hud.toggleHelp = ToggleHelp;
            hud.toggleCard = ToggleCard;
            model = document.GetComponent<UIDocumentComponent>().GetModel("hud");
            model.Bind(hud);
        }

        public override void OnUpdate()
        {
            UpdateInputKind();
            UpdateKeys();
            UpdateStation();
            UpdateCard();
            UpdateAim();
            UpdateTouchControls();
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

        /// <summary>A touch screen can turn up while playing (a touch laptop's first finger): the texts follow it.</summary>
        private void UpdateInputKind()
        {
            if (hud.touch == Input.touchSupported)
            {
                return;
            }
            hud.touch = Input.touchSupported;
            ShowStation(current);
            lastAim = null;
            model.DirtyAll();
        }

        private void UpdateKeys()
        {
            if (Input.IsPressed(KeyCode.F1))
            {
                ToggleHelp();
            }
            if (Input.IsPressed(KeyCode.Tab))
            {
                ToggleMenu();
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

        private void ToggleHelp()
        {
            hud.helpOpen = !hud.helpOpen;
            hud.menuOpen = false;
            SetCursorFree(hud.helpOpen);
            model.Dirty("helpOpen");
            model.Dirty("menuOpen");
        }

        private void ToggleMenu()
        {
            hud.menuOpen = !hud.menuOpen;
            hud.helpOpen = false;
            SetCursorFree(hud.menuOpen);
            model.Dirty("helpOpen");
            model.Dirty("menuOpen");
        }

        /// <summary>Frees the cursor for a menu and locks it to the view again; a touch screen has no cursor to hold.</summary>
        private static void SetCursorFree(bool free)
        {
            if (Input.touchSupported)
            {
                return;
            }
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
            if (nearest != current)
            {
                ShowStation(nearest);
            }
        }

        private void ShowStation(Station station)
        {
            current = station;
            hud.hasStation = current != null;
            hud.cardFolded = false;
            cardOpenedAt = Time.time;
            hud.title = current != null ? current.title : "";
            hud.description = current != null ? current.description : "";
            hud.controls = "";
            if (current != null)
            {
                hud.controls = hud.touch && !string.IsNullOrEmpty(current.touchControls) ? current.touchControls : current.controls;
            }
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
                text = hud.touch ? "Throw it, or drop it here" : "Click to throw, release to drop, wheel to move it";
            }
            else if (physicsTool.aim.HasValue)
            {
                var hit = physicsTool.aim.Value;
                var interactable = Interactable.Find(hit.entity);
                var body = hit.entity.GetComponent<PhysicsComponent>();
                if (interactable != null)
                {
                    kind = "use";
                    text = interactable.prompt + (hud.touch ? " - press Use" : " - click or press E");
                }
                else if (RagdollCharacter.Find(hit.entity) != null)
                {
                    kind = "ragdoll";
                    text = hud.touch ? "Push to shove it" : "Click to push";
                }
                else if (body != null && body.bodyType == RigidbodyType.Dynamic)
                {
                    bool heavy = body.mass > physicsTool.maxCarryMass;
                    kind = heavy ? "heavy" : "body";
                    string how = hud.touch ? "push it, or grab it to carry" : "click to push, hold right to carry";
                    text = heavy ? string.Format("{0:0} kg - too heavy to carry", body.mass)
                                 : string.Format("{0:0.#} kg - {1}", body.mass, how);
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

        /// <summary>On a touch screen the card leaves the view to the game once it has been read.</summary>
        private void UpdateCard()
        {
            if (hud.touch && hud.hasStation && !hud.cardFolded && Time.time - cardOpenedAt > cardSeconds)
            {
                hud.cardFolded = true;
                model.Dirty("cardFolded");
            }
        }

        private void ToggleCard()
        {
            hud.cardFolded = !hud.cardFolded;
            cardOpenedAt = Time.time;
            model.Dirty("cardFolded");
        }

        /// <summary>Names the on-screen buttons for what they do now, and hides the controls behind a menu.</summary>
        private void UpdateTouchControls()
        {
            if (touchJoystick == null)
            {
                return;
            }
            bool isMenuOpen = hud.menuOpen || hud.helpOpen;
            if (touchControls.activeLocal == isMenuOpen)
            {
                touchControls.SetActive(!isMenuOpen);
            }
            if (hud.driving)
            {
                SetButtons("Brake", "", "Exit", "Flip");
            }
            else if (physicsTool.carriedBody)
            {
                SetButtons("Jump", "Throw", "Use", "Drop");
            }
            else
            {
                SetButtons("Jump", "Push", "Use", "Grab");
            }
        }

        /// <summary>The bottom, right, left and top buttons: the actions Jump, Push, Use and Grab (Flip in the buggy).</summary>
        private void SetButtons(string south, string east, string west, string north)
        {
            touchJoystick.SouthLabel = south;
            touchJoystick.EastLabel = east;
            touchJoystick.WestLabel = west;
            touchJoystick.NorthLabel = north;
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
