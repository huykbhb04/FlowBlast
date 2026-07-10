using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Runtime progress label attached to a bottom box. Subscribes to its
    /// <see cref="BoxContainer"/> and shows a "%" string while the box is
    /// being filled. Hidden by default; shown the instant a matching ball hits
    /// the gate (i.e. on the very first AddProgress).
    ///
    /// Uses Unity's legacy <see cref="TextMesh"/> so we don't need an extra
    /// UI / TextMeshPro setup in the box prefab.
    /// </summary>
    public class BoxProgressDisplay : MonoBehaviour
    {
        [Header("Label layout")]
        [Tooltip("Anchored below the box (at its feet) so the player reads it without losing the box. " +
                 "Negative Y = below the box pivot.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.6f, 0f);

        [Tooltip("TextMesh.characterSize (0..1). Smaller = physically smaller text in world space.")]
        [SerializeField, Range(0.02f, 0.5f)] private float characterSize = 0.12f;

        [Tooltip("Font pixel size used by TextMesh. Drives how readable the glyphs look.")]
        [SerializeField, Range(8, 256)] private int fontSize = 64;

        [Tooltip("Color tint for the percent text.")]
        [SerializeField] private Color textColor = Color.white;

        [Header("Background quad")]
        [Tooltip("Width of the dark backing plate in local space.")]
        [SerializeField] private float quadWidth = 0.45f;

        [Tooltip("Height of the dark backing plate in local space.")]
        [SerializeField] private float quadHeight = 0.18f;

        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private MeshRenderer _outlineRenderer;
        private bool _isVisible;

        /// <summary>
        /// Attach (or reuse) a label on this box and bind it to the supplied container.
        /// </summary>
        public void Bind(BoxContainer container)
        {
            if (container == null) return;

            EnsureLabel();

            // Always start hidden; wait for the first real fill to show it.
            SetLabelVisible(false);

            container.OnProgressChanged -= HandleProgressChanged;
            container.OnProgressChanged += HandleProgressChanged;
            container.OnCompleted -= HandleProgressChanged;
            container.OnCompleted += HandleProgressChanged;
        }

        private void OnDestroy()
        {
            // BoxContainer is a [Serializable] struct held only by the BoxSlot.
            // We don't hold the container, so there is nothing to unsubscribe.
        }

        private void EnsureLabel()
        {
            if (_label != null) return;

            // Background quad behind the text so it reads against any box color.
            GameObject bg = new GameObject("ProgressLabel_BG");
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = localOffset;
            bg.transform.localRotation = Quaternion.identity;
            _outlineRenderer = bg.AddComponent<MeshRenderer>();
            MeshFilter bgMesh = bg.AddComponent<MeshFilter>();
            bgMesh.sharedMesh = BuildQuadMesh(quadWidth, quadHeight);
            _outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _outlineRenderer.receiveShadows = false;

            // 3D text. Camera is a top-down angled view, so lay the text flat
            // (rotated around X) and tucked under the box.
            GameObject label = new GameObject("ProgressLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localOffset;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _label = label.AddComponent<TextMesh>();
            _label.text = "0%";
            _label.color = textColor;
            _label.fontSize = fontSize;
            _label.characterSize = characterSize;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontStyle = FontStyle.Bold;
            _label.richText = false;

            _labelRenderer = label.GetComponent<MeshRenderer>();
            _labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _labelRenderer.receiveShadows = false;

            // Pick a shader we know is in the project (URP) and fall back to legacy.
            Shader bgShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (bgShader == null) bgShader = Shader.Find("Unlit/Texture");
            if (bgShader == null) bgShader = Shader.Find("Sprites/Default");
            _outlineRenderer.sharedMaterial = new Material(bgShader)
            {
                color = new Color(0f, 0f, 0f, 0.65f),
            };

            // The label itself just uses the built-in font material; tint via _label.color.
            _labelRenderer.sharedMaterial = _label.font.material;

            SetLabelVisible(false);
        }

        private void HandleProgressChanged(BoxContainer container)
        {
            if (container == null || _label == null) return;

            int percent = Mathf.Clamp(Mathf.RoundToInt(container.Progress), 0, 100);
            _label.text = percent + "%";

            if (!_isVisible && container.Progress > 0f)
            {
                SetLabelVisible(true);
            }

            if (container.IsCompleted)
            {
                SetLabelVisible(false);
            }
        }

        private void SetLabelVisible(bool visible)
        {
            _isVisible = visible;
            if (_labelRenderer != null) _labelRenderer.enabled = visible;
            if (_outlineRenderer != null) _outlineRenderer.enabled = visible;
            if (_label != null)
            {
                // Also disable the GameObject so coroutines/animation can't reach it.
                Transform t = _label.transform;
                if (t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);

                Transform bg = _outlineRenderer != null ? _outlineRenderer.transform : null;
                if (bg != null && bg.gameObject.activeSelf != visible) bg.gameObject.SetActive(visible);
            }
        }

        private static Mesh BuildQuadMesh(float width, float height)
        {
            Mesh m = new Mesh();
            m.vertices = new[]
            {
                new Vector3(-width, -height, 0f),
                new Vector3( width, -height, 0f),
                new Vector3( width,  height, 0f),
                new Vector3(-width,  height, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            m.RecalculateNormals();
            return m;
        }
    }
}
